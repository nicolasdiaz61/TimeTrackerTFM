using System.Net;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;
using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// «Deshabilitar, prioridad y restablecer» por compañía (TC-9): la compañía de plataforma (la demo) vuelve al entorno al
/// restablecer; cualquier otra queda con el asistente deshabilitado. Sin proveedor real: <c>RecordingChatClientFactory</c>
/// responde «respuesta de {modelo}».
/// </summary>
[Collection("Postgres")]
public sealed class AssistantResetAndPriorityTests(PostgresFixture postgres)
{
    private const string SaveOpenAi = """{"provider":"OpenAI","model":"modelo-a","concurrencyToken":"0","credential":"clave-prueba-123456"}""";

    private static Task<List<string>> Query(string connectionString, string sql) => MigrationDatabase.QueryAsync(connectionString, sql);

    private static async Task<string> TokenOfAsync(HttpClient client)
    {
        using var body = await ReadJsonAsync(await client.GetAsync(AssistantConfigurationHttp.Route, CancellationToken.None));
        return body.RootElement.GetProperty("concurrencyToken").GetString()!;
    }

    private static async Task<(string Provider, string Source, string? Model)> EffectiveAsync(HttpClient client)
    {
        using var body = await ReadJsonAsync(await client.GetAsync(AssistantConfigurationHttp.Route, CancellationToken.None));
        var root = body.RootElement;
        return (root.GetProperty("provider").GetString()!, root.GetProperty("sources").GetProperty("provider").GetString()!, root.GetProperty("model").GetString());
    }

    private static async Task<(HttpStatusCode Status, string? Reply)> ChatAsync(HttpClient client)
    {
        var response = await client.AskAsync("hola");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, null);
        }

        using var body = await ReadAsync(response);
        return (response.StatusCode, body.RootElement.GetProperty("reply").GetString());
    }

    [Fact]
    public async Task ResetInAnotherCompany_RemovesTheRow_ShowsNoneWithDefaultOrigin_AndTheChatStaysDisabled()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);
        Assert.Equal(HttpStatusCode.OK, (await managerA.PutJsonAsync(SaveOpenAi)).StatusCode);

        var reset = await managerA.DeleteAsync(AssistantConfigurationHttp.Route, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(["0"], await Query(fixture.ConnectionString, $"SELECT count(*)::text FROM assistant_settings WHERE company_id = '{fixture.A.Id}'"));
        Assert.Equal(("None", "default", null), await EffectiveAsync(managerA));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await ChatAsync(TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Employee1))).Status);
        Assert.Equal(["Saved", "Reset"], await Query(fixture.ConnectionString, $"SELECT action FROM assistant_settings_audit WHERE company_id = '{fixture.A.Id}' ORDER BY occurred_at"));
    }

    [Fact]
    public async Task ResetOfA_LeavesTheRowAndTheXminOfBUntouched_AndWritesNoAuditForB()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);
        var managerB = TwoCompanyFixture.ClientFor(factory, fixture.B, fixture.B.Manager);
        Assert.Equal(HttpStatusCode.OK, (await managerA.PutJsonAsync(SaveOpenAi)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await managerB.PutJsonAsync(SaveOpenAi.Replace("modelo-a", "modelo-b", StringComparison.Ordinal))).StatusCode);
        var xminOfB = await Query(fixture.ConnectionString, $"SELECT xmin::text FROM assistant_settings WHERE company_id = '{fixture.B.Id}'");
        var auditOfB = await Query(fixture.ConnectionString, $"SELECT count(*)::text FROM assistant_settings_audit WHERE company_id = '{fixture.B.Id}'");

        Assert.Equal(HttpStatusCode.NoContent, (await managerA.DeleteAsync(AssistantConfigurationHttp.Route, CancellationToken.None)).StatusCode);

        Assert.Equal(xminOfB, await Query(fixture.ConnectionString, $"SELECT xmin::text FROM assistant_settings WHERE company_id = '{fixture.B.Id}'"));
        Assert.Equal(auditOfB, await Query(fixture.ConnectionString, $"SELECT count(*)::text FROM assistant_settings_audit WHERE company_id = '{fixture.B.Id}'"));
        Assert.Equal(("OpenAI", "stored", "modelo-b"), await EffectiveAsync(managerB));
    }

    [Fact]
    public async Task ResetWithAStaleToken_IsAConflictAndKeepsTheNewRowWithoutNewAudit()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        var audit = new TimeTracker.Application.Assistant.Configuration.AuditEntry(
            Guid.NewGuid(), DateTimeOffset.UtcNow, TimeTracker.Application.Assistant.Configuration.AuditAction.Reset, CredentialChanged: false, Previous: null, Current: null);
        var settings = new TimeTracker.Application.Assistant.Configuration.AssistantSettings(
            TimeTracker.Application.Assistant.Configuration.AssistantProvider.Ollama, "m-1", null, null, null, null, Guid.NewGuid(), DateTimeOffset.UtcNow, "0");
        var read = Assert.IsType<TimeTracker.Application.Assistant.Configuration.SaveOutcome.Saved>(
            await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(settings, "0", audit, CancellationToken.None)).Token;
        Assert.IsType<TimeTracker.Application.Assistant.Configuration.SaveOutcome.Saved>(
            await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(settings with { Model = "m-2" }, read, audit, CancellationToken.None));
        var auditBefore = await Query(cs, "SELECT count(*)::text FROM assistant_settings_audit");

        var outcome = await postgres.CreateAssistantStore(cs, fixture.A.Id).DeleteAsync(read, audit, CancellationToken.None);

        Assert.Equal(TimeTracker.Application.Assistant.Configuration.DeleteOutcome.Conflict, outcome);
        Assert.Equal(["m-2"], await Query(cs, $"SELECT model FROM assistant_settings WHERE company_id = '{fixture.A.Id}'"));
        Assert.Equal(auditBefore, await Query(cs, "SELECT count(*)::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task WithoutTheSettingsTable_TheDemoUsesTheEnvironmentWithoutError_AndAnyOtherCompanyIsDisabled()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        await MigrationDatabase.ExecuteAsync(fixture.ConnectionString, "DROP TABLE assistant_settings");
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var demo = factory.CreateClientFor(Guid.NewGuid(), "Manager");
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);

        Assert.Equal(("Ollama", "env", AssistantConfigurationApiFactory.EnvironmentModel), await EffectiveAsync(demo));
        Assert.Equal(("None", "default", null), await EffectiveAsync(managerA));
        Assert.Equal((HttpStatusCode.OK, $"respuesta de {AssistantConfigurationApiFactory.EnvironmentModel}"), await ChatAsync(demo));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await ChatAsync(TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Employee1))).Status);
    }

    [Fact]
    public async Task InTheDemo_ASavedConfigurationWinsOverTheEnvironment_AndResetGoesBackToTheEnvironment()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var demo = factory.CreateClientFor(Guid.NewGuid(), "Manager");
        Assert.Equal((HttpStatusCode.OK, "respuesta de modelo-env"), await ChatAsync(demo));

        Assert.Equal(HttpStatusCode.OK, (await demo.PutJsonAsync("""{"provider":"Ollama","model":"db-m","concurrencyToken":"0"}""")).StatusCode);
        Assert.Equal(("Ollama", "stored", "db-m"), await EffectiveAsync(demo));
        Assert.Equal((HttpStatusCode.OK, "respuesta de db-m"), await ChatAsync(demo));

        Assert.Equal(HttpStatusCode.NoContent, (await demo.DeleteAsync(AssistantConfigurationHttp.Route, CancellationToken.None)).StatusCode);
        Assert.Equal(("Ollama", "env", "modelo-env"), await EffectiveAsync(demo));
        Assert.Equal((HttpStatusCode.OK, "respuesta de modelo-env"), await ChatAsync(demo));
        Assert.Equal([Company.DefaultId.ToString(), Company.DefaultId.ToString()], await Query(fixture.ConnectionString, "SELECT company_id::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task InTheDemo_AStoredNone_DisablesTheChat()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var demo = factory.CreateClientFor(Guid.NewGuid(), "Manager");

        Assert.Equal(HttpStatusCode.OK, (await demo.PutJsonAsync("""{"provider":"None","concurrencyToken":"0"}""")).StatusCode);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await ChatAsync(demo)).Status);
        Assert.Equal(("None", "stored", AssistantConfigurationApiFactory.EnvironmentModel), await EffectiveAsync(demo));
    }

    [Fact]
    public async Task ResetWithoutARow_Returns204AndInsertsNoAudit()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);

        var reset = await managerA.DeleteAsync(AssistantConfigurationHttp.Route, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(["0"], await Query(fixture.ConnectionString, "SELECT count(*)::text FROM assistant_settings_audit"));
        Assert.Equal("0", await TokenOfAsync(managerA));
    }
}
