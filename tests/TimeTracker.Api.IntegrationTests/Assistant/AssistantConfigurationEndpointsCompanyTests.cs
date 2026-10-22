using System.Net;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// <c>/api/asistente/configuracion</c> con dos compañías (TC-9): cada Manager solo ve, edita, restablece y prueba la fila de la
/// suya; la compañía sale del token y no existe ningún identificador de compañía en el contrato. Sin proveedor real: fábrica y
/// probador falsos.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantConfigurationEndpointsCompanyTests(PostgresFixture postgres)
{
    private const string ValidPut = """{"provider":"OpenAI","model":"modelo-a","timeoutSeconds":20,"maxOutputTokens":500,"credential":"clave-prueba-123456","concurrencyToken":"0"}""";

    private static Task<List<string>> Query(string connectionString, string sql) => MigrationDatabase.QueryAsync(connectionString, sql);

    private static async Task<JsonDocument> GetAsync(HttpClient client)
    {
        var response = await client.GetAsync(Route, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    [Fact]
    public async Task ACompanyWithoutARow_SeesTheAssistantDisabled_WithDefaultOriginsAndNoPlatformValues()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);

        using var body = await GetAsync(TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager));

        var root = body.RootElement;
        Assert.Equal("None", root.GetProperty("provider").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("model").ValueKind);
        Assert.False(root.GetProperty("credentialConfigured").GetBoolean());
        var sources = root.GetProperty("sources");
        Assert.Equal("default", sources.GetProperty("provider").GetString());
        Assert.Equal("default", sources.GetProperty("model").GetString());
        Assert.Equal("none", sources.GetProperty("credential").GetString());
        Assert.Equal("env", sources.GetProperty("timeoutSeconds").GetString());
        Assert.Equal("env", sources.GetProperty("maxOutputTokens").GetString());
        Assert.DoesNotContain(AssistantConfigurationApiFactory.EnvironmentModel, root.GetRawText(), StringComparison.Ordinal);
        Assert.False(root.TryGetProperty("companyId", out _));
        Assert.Equal("0", root.GetProperty("concurrencyToken").GetString());
    }

    [Fact]
    public async Task TheDemoCompanyWithoutARow_StillSeesTheEnvironment()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);

        using var body = await GetAsync(factory.CreateClientFor(Guid.NewGuid(), "Manager"));

        Assert.Equal("Ollama", body.RootElement.GetProperty("provider").GetString());
        Assert.Equal(AssistantConfigurationApiFactory.EnvironmentModel, body.RootElement.GetProperty("model").GetString());
        Assert.Equal("env", body.RootElement.GetProperty("sources").GetProperty("provider").GetString());
    }

    [Fact]
    public async Task EachManagerSavesAndReadsOnlyTheRowOfItsCompany_AndTheOtherNeverSeesIt()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);
        var managerB = TwoCompanyFixture.ClientFor(factory, fixture.B, fixture.B.Manager);

        var savedByA = await managerA.PutJsonAsync(ValidPut);
        Assert.Equal(HttpStatusCode.OK, savedByA.StatusCode);

        using var seenByA = await GetAsync(managerA);
        using var seenByB = await GetAsync(managerB);
        Assert.Equal("modelo-a", seenByA.RootElement.GetProperty("model").GetString());
        Assert.Equal("stored", seenByA.RootElement.GetProperty("sources").GetProperty("model").GetString());
        Assert.True(seenByA.RootElement.GetProperty("credentialConfigured").GetBoolean());
        var rawB = seenByB.RootElement.GetRawText();
        Assert.DoesNotContain("modelo-a", rawB, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-prueba-123456", rawB, StringComparison.Ordinal);
        Assert.Equal("None", seenByB.RootElement.GetProperty("provider").GetString());
        Assert.False(seenByB.RootElement.GetProperty("credentialConfigured").GetBoolean());
        Assert.Equal([fixture.A.Id.ToString()], await Query(fixture.ConnectionString, "SELECT company_id::text FROM assistant_settings"));
        Assert.Equal([fixture.A.Id.ToString()], await Query(fixture.ConnectionString, "SELECT company_id::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task AnyManagerOfTheCompanyEditsItsConfiguration_ButTheKeyIsNeverReturned()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var secondManager = new TwoCompanyFixture.Member(Guid.NewGuid(), $"manager2.{Guid.NewGuid():N}@example.test", TimeTracker.Domain.Users.UserRole.Manager);
        await UserSeed.InsertRawAsync(fixture.ConnectionString, secondManager.Id, secondManager.Email, "Manager", companyId: fixture.A.Id);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var first = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);
        var second = TwoCompanyFixture.ClientFor(factory, fixture.A, secondManager);
        Assert.Equal(HttpStatusCode.OK, (await first.PutJsonAsync(ValidPut)).StatusCode);

        using var seenBySecond = await GetAsync(second);
        var token = seenBySecond.RootElement.GetProperty("concurrencyToken").GetString();
        var edited = await second.PutJsonAsync($$"""{"provider":"OpenAI","model":"modelo-a2","concurrencyToken":"{{token}}"}""");

        Assert.Equal("modelo-a", seenBySecond.RootElement.GetProperty("model").GetString());
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.DoesNotContain("clave-prueba-123456", await edited.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
        Assert.Equal(["modelo-a2"], await Query(fixture.ConnectionString, $"SELECT model FROM assistant_settings WHERE company_id = '{fixture.A.Id}'"));
    }

    [Fact]
    public async Task TheKeyOfOneCompany_IsNeverUsableByTheOther_ForTheConnectionTest()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);
        var managerB = TwoCompanyFixture.ClientFor(factory, fixture.B, fixture.B.Manager);
        Assert.Equal(HttpStatusCode.OK, (await managerA.PutJsonAsync(ValidPut)).StatusCode);

        var emptyBody = await managerB.PostTestAsync();
        var sameProviderWithoutKey = await managerB.PostTestAsync("""{"provider":"OpenAI","model":"modelo-b"}""");

        Assert.Equal(HttpStatusCode.BadRequest, emptyBody.StatusCode);
        Assert.Contains("provider", (await FieldErrorsAsync(emptyBody)).Keys);
        Assert.Equal(HttpStatusCode.BadRequest, sameProviderWithoutKey.StatusCode);
        Assert.Contains("credential", (await FieldErrorsAsync(sameProviderWithoutKey)).Keys);
        Assert.Empty(factory.Tester.Candidates);

        // La compañía A sí prueba con su propia clave guardada.
        var own = await managerA.PostTestAsync("""{"provider":"OpenAI","model":"modelo-a"}""");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("clave-prueba-123456", Assert.Single(factory.Tester.Candidates).Credential!.Value);
    }

    [Fact]
    public async Task TheTestOfANonPlatformCompany_NeverUsesThePlatformKey_ButTheDemoDoes()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);
        var demo = factory.CreateClientFor(Guid.NewGuid(), "Manager");

        var inA = await managerA.PostTestAsync("""{"provider":"Ollama","model":"modelo-a"}""");
        var inDemo = await demo.PostTestAsync();

        // Ollama no necesita clave: lo que se comprueba es que a A no le llega ninguna clave de plataforma.
        Assert.Equal(HttpStatusCode.OK, inA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inDemo.StatusCode);
        Assert.Equal(2, factory.Tester.Candidates.Count);
        Assert.Null(factory.Tester.Candidates[0].Credential);
        Assert.Equal(AssistantConfigurationApiFactory.EnvironmentModel, factory.Tester.Candidates[1].Model);
    }

    [Fact]
    public async Task TheContract_HasNoCompanyIdentifier_AndABodyThatTriesToSelectOneIsRejectedWithoutWriting()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);

        var put = await managerA.PutJsonAsync($$"""{"provider":"None","concurrencyToken":"0","companyId":"{{fixture.B.Id}}"}""");
        var query = await managerA.GetAsync($"{Route}?companyId={fixture.B.Id}", CancellationToken.None);
        var header = new HttpRequestMessage(HttpMethod.Get, Route);
        header.Headers.Add("X-Company-Id", fixture.B.Id.ToString());
        var viaHeader = await managerA.SendAsync(header, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Equal(["0"], await Query(fixture.ConnectionString, "SELECT count(*)::text FROM assistant_settings"));
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
        Assert.Equal(HttpStatusCode.OK, viaHeader.StatusCode);
        Assert.DoesNotContain(fixture.B.Id.ToString(), await query.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("POST")]
    public async Task AnEmployeeOfACompany_Gets403OnEveryEndpoint(string method)
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var employee = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Employee1);
        var route = method == "POST" ? TestRoute : Route;
        using var request = new HttpRequestMessage(new HttpMethod(method), route) { Content = method is "PUT" or "POST" ? Json("{}") : null };

        var response = await employee.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(["0"], await Query(fixture.ConnectionString, "SELECT count(*)::text FROM assistant_settings"));
    }
}
