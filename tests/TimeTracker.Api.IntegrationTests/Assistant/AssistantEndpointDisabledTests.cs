using System.Net;
using System.Net.Http.Headers;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Infrastructure.Tests.Fakes;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// «Asistente deshabilitado por compañía» con el puente transitorio activo (PR 4a, DD-14): el runtime singleton solo sirve a la
/// compañía de plataforma (la demo). Cualquier otra recibe el 503 genérico sin consultar al modelo, tenga o no fila propia, y
/// jamás la instantánea ni la clave de plataforma. La 4b sustituye el puente por el registro de instantáneas por compañía.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantEndpointDisabledTests(PostgresFixture postgres)
{
    private static readonly Guid AnotherCompany = Guid.Parse("33333333-3333-7333-8333-333333333333");

    private static HttpClient ClientOf(ApiFactory factory, Guid companyId)
    {
        var client = factory.CreateDefaultClient();
        var token = TestTokens.Create(Guid.NewGuid(), "Employee", factory.Clock.GetUtcNow(), companyId: companyId.ToString(), companyName: "otra compania");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ACompanyWithoutPlatformAssistant_GetsTheGeneric503_AndTheModelIsNeverConsulted()
    {
        var model = new FakeChatClient(Reply("no debería llegar"));
        using var factory = new AssistantApiFactory(model);

        var response = await ClientOf(factory, AnotherCompany).AskAsync("hola");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(DisabledDetail, await ProblemDetailAsync(response, 503));
        Assert.Empty(model.Calls);
    }

    [Fact]
    public async Task TheDemoCompany_KeepsTheCurrentBehavior()
    {
        var model = new FakeChatClient(Reply("Hola desde la demo"));
        using var factory = new AssistantApiFactory(model);

        var response = await factory.CreateClient().AskAsync("hola");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal("Hola desde la demo", body.RootElement.GetProperty("reply").GetString());
        Assert.Single(model.Calls);
    }

    [Fact]
    public async Task ACompanyWithItsOwnRow_StillGetsTheDisabled503_AndNeitherItsSnapshotNorThePlatformKeyIsBuilt()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);
        var employeeA = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Employee1);
        var saved = await managerA.PutAsync(
            AssistantConfigurationHttp.Route,
            AssistantConfigurationHttp.Json("""{"provider":"OpenAI","model":"modelo-a","concurrencyToken":"0","credential":"clave-prueba-123456"}"""),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var buildsBefore = factory.ChatFactory.Created.Count;

        var asEmployee = await employeeA.AskAsync("hola");
        var asManager = await managerA.AskAsync("hola");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, asEmployee.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, asManager.StatusCode);
        Assert.Equal(DisabledDetail, await ProblemDetailAsync(asEmployee, 503));
        Assert.Equal(buildsBefore, factory.ChatFactory.Created.Count);
        Assert.DoesNotContain(factory.ChatFactory.Created, options => options.Model == "modelo-a" || options.ApiKey == "clave-prueba-123456");
    }

    [Fact]
    public async Task TheDemoSavesAnOwnConfiguration_AndTheOtherCompanyStaysDisabledWithoutLearningIt()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var demo = factory.CreateClientFor(Guid.NewGuid(), "Manager");
        var employeeB = TwoCompanyFixture.ClientFor(factory, fixture.B, fixture.B.Employee1);
        var saved = await demo.PutAsync(
            AssistantConfigurationHttp.Route,
            AssistantConfigurationHttp.Json("""{"provider":"Ollama","model":"modelo-demo","concurrencyToken":"0"}"""),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var inDemo = await demo.AskAsync("hola");
        var inB = await employeeB.AskAsync("hola");

        using var body = await ReadAsync(inDemo);
        Assert.Equal("respuesta de modelo-demo", body.RootElement.GetProperty("reply").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, inB.StatusCode);
        Assert.DoesNotContain("modelo-demo", await inB.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
    }
}
