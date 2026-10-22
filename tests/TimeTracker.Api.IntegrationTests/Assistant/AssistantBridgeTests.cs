using System.Net;

using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;
using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// Compañía por defecto (la demo) de extremo a extremo: con la clave primaria <c>company_id</c> el almacén lee y guarda su fila y la
/// configuración guardada llega al registro de instantáneas (el puente de la 1b se retiró en la 4b). Sin llamadas a ningún proveedor
/// real: el proveedor es una fábrica falsa que responde «respuesta de {modelo}».
/// </summary>
[Collection("Postgres")]
public sealed class AssistantBridgeTests(PostgresFixture postgres)
{
    private static readonly string MasterKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

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

    [Fact]
    public async Task TheStore_SavesAndReadsTheRowOfTheDefaultCompanyWithCompanyIdAsTheOnlyKey()
    {
        var connectionString = await MigratedDatabaseAsync();
        var audit = new AuditEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, AuditAction.Saved, CredentialChanged: false, Previous: null, Current: null);
        var next = new AssistantSettings(AssistantProvider.Ollama, "modelo-puente", 25, 800, AssistantEffort.Low, Credential: null, audit.ActorId, audit.OccurredAt, "0");

        var saved = await postgres.CreateAssistantStore(connectionString).SaveAsync(next, "0", audit, CancellationToken.None);

        Assert.IsType<SaveOutcome.Saved>(saved);
        var read = await postgres.CreateAssistantStore(connectionString).GetAsync(CancellationToken.None);
        Assert.Equal("modelo-puente", read!.Model);
        Assert.Equal([$"{Company.DefaultId}|modelo-puente"], await MigrationDatabase.QueryAsync(connectionString, "SELECT company_id || '|' || model FROM assistant_settings"));
        Assert.Equal(["company_id"], await MigrationDatabase.QueryAsync(connectionString, "SELECT a.attname FROM pg_constraint c JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey) WHERE c.conrelid = 'assistant_settings'::regclass AND c.contype = 'p'"));
        Assert.Equal(["0"], await MigrationDatabase.QueryAsync(connectionString, "SELECT count(*)::text FROM information_schema.columns WHERE table_name = 'assistant_settings' AND column_name = 'id'"));
    }

    [Fact]
    public async Task TheStore_IgnoresTheRowOfAnotherCompanyAndTheAuditCarriesTheDefaultCompany()
    {
        var connectionString = await MigratedDatabaseAsync();
        var other = await CompanySeed.InsertAsync(connectionString);
        await MigrationDatabase.ExecuteAsync(connectionString, $"INSERT INTO assistant_settings (company_id, provider, model, updated_by, updated_at) VALUES ('{other}', 'Ollama', 'modelo-ajeno', gen_random_uuid(), now())");
        var store = postgres.CreateAssistantStore(connectionString);

        Assert.Null(await store.GetAsync(CancellationToken.None));
        Assert.Equal("0", await store.GetVersionAsync(CancellationToken.None));

        var audit = new AuditEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, AuditAction.Saved, CredentialChanged: false, Previous: null, Current: null);
        var next = new AssistantSettings(AssistantProvider.Ollama, "modelo-propio", 25, 800, AssistantEffort.Low, Credential: null, audit.ActorId, audit.OccurredAt, "0");
        Assert.IsType<SaveOutcome.Saved>(await postgres.CreateAssistantStore(connectionString).SaveAsync(next, "0", audit, CancellationToken.None));

        Assert.Equal([$"{Company.DefaultId}|modelo-propio", $"{other}|modelo-ajeno"], await MigrationDatabase.QueryAsync(connectionString, "SELECT company_id || '|' || model FROM assistant_settings ORDER BY model DESC"));
        Assert.Equal([Company.DefaultId.ToString()], await MigrationDatabase.QueryAsync(connectionString, "SELECT company_id::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task ASaveThroughTheApi_ReachesTheSingletonRuntimeAndASecondInstanceAfterItsPoll()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var first = new ReloadApiFactory(connectionString, applyMigrations: true, masterKey: MasterKey);
        using var second = new ReloadApiFactory(connectionString, masterKey: MasterKey);
        var secondChat = second.CreateClient();
        Assert.Equal("respuesta de modelo-env", await ReplyAsync(secondChat));

        var saved = await first.CreateClientFor(Guid.NewGuid(), "Manager").PutJsonAsync("""{"provider":"Ollama","model":"modelo-guardado","concurrencyToken":"0"}""");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        Assert.Equal("respuesta de modelo-guardado", await ReplyAsync(first.CreateClient()));
        await second.PollOnceAsync();
        Assert.Equal("respuesta de modelo-guardado", await ReplyAsync(secondChat));
        Assert.Equal([Company.DefaultId.ToString()], await MigrationDatabase.QueryAsync(connectionString, "SELECT company_id::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task WithoutARow_TheEnvironmentKeepsServingTheDefaultCompany_AndAStoredKeyWinsOverIt()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new ReloadApiFactory(connectionString, applyMigrations: true, masterKey: MasterKey);
        var chat = factory.CreateClient();

        Assert.Equal("respuesta de modelo-env", await ReplyAsync(chat));
        Assert.All(factory.ChatFactory.Created, options => Assert.Equal("modelo-env", options.Model));

        var saved = await factory.CreateClientFor(Guid.NewGuid(), "Manager").PutJsonAsync("""{"provider":"OpenAI","model":"modelo-openai","credential":"clave-prueba-123456","concurrencyToken":"0"}""");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        Assert.Equal("respuesta de modelo-openai", await ReplyAsync(chat));
        Assert.Equal("clave-prueba-123456", factory.ChatFactory.Created[^1].ApiKey);
    }

    private async Task<string> MigratedDatabaseAsync()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var context = postgres.CreateContext(connectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        return connectionString;
    }
}
