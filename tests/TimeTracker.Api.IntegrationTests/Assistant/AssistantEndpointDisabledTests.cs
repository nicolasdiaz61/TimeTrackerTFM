using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.AI;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Infrastructure.Tests.Fakes;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// «Asistente deshabilitado por compañía» (DD-14): una compañía sin instantánea con proveedor recibe el 503 genérico sin consultar al
/// modelo y la configuración de una compañía no la ve otra. Las pruebas con varias compañías y filas reales están en
/// <c>AssistantRuntimeIsolationTests</c>.
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
        using var factory = new AssistantApiFactory(model, modelsByCompany: new Dictionary<Guid, IChatClient?> { [AnotherCompany] = null });

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
