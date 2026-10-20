using System.Net;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// V-6 y DD-11 con PostgreSQL real: un guardado externo (SQL, como haría otra instancia) llega al chat con el sondeo de 1 s,
/// sin reiniciar y sin llamar a ningún proveedor real.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantSettingsExternalSaveTests(PostgresFixture postgres)
{
    private static async Task<string?> ReplyAsync(HttpClient client)
    {
        var response = await client.AskAsync("hola");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            return null;
        }

        using var body = await ReadAsync(response);
        return body.RootElement.GetProperty("reply").GetString();
    }

    [Fact]
    public async Task AnExternalSave_ChangesTheModelOfTheChatAfterThePoll()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new ReloadApiFactory(connectionString, applyMigrations: true);
        var client = factory.CreateClient();
        Assert.Equal("respuesta de modelo-env", await ReplyAsync(client));

        await ReloadApiFactory.ExecuteAsync(connectionString, "INSERT INTO assistant_settings (id, provider, model, updated_by, updated_at) VALUES (1, 'Ollama', 'modelo-nuevo', gen_random_uuid(), now())");
        await factory.PollOnceAsync();
        Assert.Equal("respuesta de modelo-nuevo", await ReplyAsync(client));

        await ReloadApiFactory.ExecuteAsync(connectionString, "UPDATE assistant_settings SET model = 'modelo-tercero', updated_at = now()");
        await factory.PollOnceAsync();
        Assert.Equal("respuesta de modelo-tercero", await ReplyAsync(client));
    }

    [Fact]
    public async Task AnExternalNoneRow_DisablesTheChatWith503_AndDeletingItRestoresTheEnvironment()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new ReloadApiFactory(connectionString, applyMigrations: true);
        var client = factory.CreateClient();

        await ReloadApiFactory.ExecuteAsync(connectionString, "INSERT INTO assistant_settings (id, provider, updated_by, updated_at) VALUES (1, 'None', gen_random_uuid(), now())");
        await factory.PollOnceAsync();
        var disabled = await client.AskAsync("hola");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, disabled.StatusCode);
        Assert.Equal(DisabledDetail, await ProblemDetailAsync(disabled, 503));

        await ReloadApiFactory.ExecuteAsync(connectionString, "DELETE FROM assistant_settings");
        await factory.PollOnceAsync();
        Assert.Equal("respuesta de modelo-env", await ReplyAsync(client));
    }

    [Fact]
    public async Task AnInvalidExternalRow_KeepsTheLastValidConfiguration()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new ReloadApiFactory(connectionString, applyMigrations: true);
        var client = factory.CreateClient();

        // Anthropic exige clave y el entorno tampoco la tiene: la fila no es utilizable y se conserva el entorno.
        await ReloadApiFactory.ExecuteAsync(connectionString, "INSERT INTO assistant_settings (id, provider, model, updated_by, updated_at) VALUES (1, 'Anthropic', 'modelo-invalido', gen_random_uuid(), now())");
        await factory.PollOnceAsync();

        Assert.Equal("respuesta de modelo-env", await ReplyAsync(client));
        Assert.DoesNotContain(factory.ChatFactory.Created, options => options.Model == "modelo-invalido");
    }
}
