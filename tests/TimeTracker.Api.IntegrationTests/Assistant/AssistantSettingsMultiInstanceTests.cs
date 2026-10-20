using System.Net;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;
using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// DD-11 con dos instancias de la API sobre la misma base de datos real y <c>Ai__SettingsPollSeconds=1</c>. Cada instancia tiene
/// su propio reloj ficticio y espera la señal explícita de «vuelta de sondeo terminada»: no hay esperas por tiempo real ni red.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantSettingsMultiInstanceTests(PostgresFixture postgres)
{
    private const string Secret = "clave-prueba-123456";

    private static readonly string KeyOfTheFirstInstance = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
    private static readonly string KeyOfAnotherDeployment = Convert.ToBase64String(Enumerable.Range(101, 32).Select(value => (byte)value).ToArray());

    private static async Task<string?> ReplyAsync(HttpClient chat)
    {
        var response = await chat.AskAsync("hola");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            return null;
        }

        using var body = await ReadAsync(response);
        return body.RootElement.GetProperty("reply").GetString();
    }

    private static HttpClient Manager(ReloadApiFactory factory) => factory.CreateClientFor(Guid.NewGuid(), "Manager");

    private static async Task<string> SaveAsync(HttpClient manager, string json)
    {
        var response = await manager.PutJsonAsync(json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("concurrencyToken").GetString()!;
    }

    private static int RejectionWarnings(ReloadApiFactory factory, string category)
        => factory.Logs.Messages.Count(message => message.Contains($"motivo {category}", StringComparison.Ordinal));

    private static void AssertNoSecrets(ReloadApiFactory factory)
        => Assert.DoesNotContain(factory.Logs.Messages, message => message.Contains(Secret, StringComparison.Ordinal)
            || message.Contains(KeyOfTheFirstInstance, StringComparison.Ordinal)
            || message.Contains(KeyOfAnotherDeployment, StringComparison.Ordinal));

    [Fact]
    public async Task ASaveOnTheFirstInstance_IsUsedByTheSecondOneAfterItsNextPoll()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var first = new ReloadApiFactory(connectionString, applyMigrations: true, masterKey: KeyOfTheFirstInstance);
        using var second = new ReloadApiFactory(connectionString, masterKey: KeyOfTheFirstInstance);
        var secondChat = second.CreateClient();
        Assert.Equal("respuesta de modelo-env", await ReplyAsync(secondChat));

        await SaveAsync(Manager(first), """{"provider":"Ollama","model":"modelo-nuevo","concurrencyToken":"0"}""");

        // La primera instancia recarga al instante; la segunda sigue con lo anterior hasta que sondea.
        Assert.Equal("respuesta de modelo-nuevo", await ReplyAsync(first.CreateClient()));
        Assert.Equal("respuesta de modelo-env", await ReplyAsync(secondChat));
        await second.PollOnceAsync();
        Assert.Equal("respuesta de modelo-nuevo", await ReplyAsync(secondChat));
    }

    [Fact]
    public async Task WithoutTheTable_TheInstanceUsesTheEnvironmentAndPicksUpTheFirstSaveWhenAnotherInstanceMigrates()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var waiting = new ReloadApiFactory(connectionString);
        var chat = waiting.CreateClient();

        Assert.Equal("respuesta de modelo-env", await ReplyAsync(chat));
        await waiting.PollOnceAsync();
        Assert.Equal("respuesta de modelo-env", await ReplyAsync(chat));

        using var migrating = new ReloadApiFactory(connectionString, applyMigrations: true, masterKey: KeyOfTheFirstInstance);
        await SaveAsync(Manager(migrating), """{"provider":"Ollama","model":"modelo-tras-migrar","concurrencyToken":"0"}""");
        await waiting.PollOnceAsync();

        Assert.Equal("respuesta de modelo-tras-migrar", await ReplyAsync(chat));
    }

    [Fact]
    public async Task ARowThatTheSecondInstanceCannotDecrypt_KeepsItsLastValidConfigurationAndWarnsOnceWithoutSecrets()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var first = new ReloadApiFactory(connectionString, applyMigrations: true, masterKey: KeyOfTheFirstInstance);
        using var second = new ReloadApiFactory(connectionString, masterKey: KeyOfAnotherDeployment);
        var secondChat = second.CreateClient();

        var token = await SaveAsync(Manager(first), $$"""{"provider":"OpenAI","model":"modelo-openai","credential":"{{Secret}}","concurrencyToken":"0"}""");
        await second.PollOnceAsync();
        await second.PollOnceAsync();

        Assert.Equal("respuesta de modelo-openai", await ReplyAsync(first.CreateClient()));
        Assert.Equal("respuesta de modelo-env", await ReplyAsync(secondChat));
        Assert.DoesNotContain(second.ChatFactory.Created, options => options.Model == "modelo-openai");
        Assert.Equal(1, RejectionWarnings(second, "undecryptable"));
        Assert.Contains(second.Logs.Messages, message => message.Contains($"versión {token},", StringComparison.Ordinal));
        AssertNoSecrets(second);
    }

    [Fact]
    public async Task ATamperedRow_IsRejectedByTheAuthenticationTag_KeepsTheLastValidOneAndRecoversWhenItIsRemoved()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var first = new ReloadApiFactory(connectionString, applyMigrations: true, masterKey: KeyOfTheFirstInstance);
        using var second = new ReloadApiFactory(connectionString, masterKey: KeyOfTheFirstInstance);
        var secondChat = second.CreateClient();
        await SaveAsync(Manager(first), $$"""{"provider":"OpenAI","model":"modelo-valido","credential":"{{Secret}}","concurrencyToken":"0"}""");
        await second.PollOnceAsync();
        Assert.Equal("respuesta de modelo-valido", await ReplyAsync(secondChat));

        await ReloadApiFactory.ExecuteAsync(
            connectionString,
            "UPDATE assistant_settings SET credential_ciphertext = set_byte(credential_ciphertext, 0, get_byte(credential_ciphertext, 0) # 255)");
        await second.PollOnceAsync();
        await second.PollOnceAsync();

        Assert.Equal("respuesta de modelo-valido", await ReplyAsync(secondChat));
        Assert.Equal(1, RejectionWarnings(second, "undecryptable"));
        AssertNoSecrets(second);

        await ReloadApiFactory.ExecuteAsync(connectionString, "DELETE FROM assistant_settings");
        await second.PollOnceAsync();
        Assert.Equal("respuesta de modelo-env", await ReplyAsync(secondChat));
    }
}
