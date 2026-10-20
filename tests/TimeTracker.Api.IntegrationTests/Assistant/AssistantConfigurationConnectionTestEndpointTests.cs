using System.Globalization;
using System.Net;

using TimeTracker.Application.Assistant.Configuration;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;
using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// <c>POST /api/asistente/configuracion/prueba</c> sobre el endpoint real, con el probador falso (nunca hay red).
/// El límite (DD-15) se comprueba aquí con la política aplicada tras autorizar: solo cuenta el Manager.
/// </summary>
public sealed class AssistantConfigurationConnectionTestEndpointTests
{
    private const string RateLimitDetail = "Ha realizado demasiadas pruebas de conexión. Espere un minuto antes de volver a intentarlo.";
    private const string InvalidBody = """{"provider":"Ollama","model":"<script>"}""";

    private static AssistantConfigurationApiFactory NewFactory(string role = "Manager") => new(role: role, store: new CountingSettingsStore());

    [Theory]
    [InlineData("ok", "Conexión correcta.")]
    [InlineData("authentication", "El proveedor rechazó la clave.")]
    [InlineData("rejected", "El proveedor rechazó los parámetros (modelo o esfuerzo no admitido).")]
    [InlineData("unreachable", "No se pudo contactar con el proveedor.")]
    public async Task Test_Returns200WithTheFixedCategoryAndMessageOfTheTester(string result, string message)
    {
        using var factory = NewFactory();
        factory.Tester.Result = new ConnectionTestResult(result, message);

        var response = await factory.CreateClient().PostTestAsync("""{"provider":"Ollama","model":"modelo-candidato"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(["result", "message"], body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal((result, message), (body.RootElement.GetProperty("result").GetString(), body.RootElement.GetProperty("message").GetString()));
        Assert.Equal("modelo-candidato", Assert.Single(factory.Tester.Candidates).Model);
    }

    [Fact]
    public async Task Test_WithoutBody_TestsTheEffectiveConfiguration()
    {
        using var factory = NewFactory();

        var response = await factory.CreateClient().PostAsync(TestRoute, content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidate = Assert.Single(factory.Tester.Candidates);
        Assert.Equal((AssistantProvider.Ollama, AssistantConfigurationApiFactory.EnvironmentModel), (candidate.Provider, candidate.Model));
    }

    [Fact]
    public async Task Test_WithAnInvalidCandidate_Returns400PerFieldWithoutContactingTheProvider()
    {
        using var factory = NewFactory();

        var response = await factory.CreateClient().PostTestAsync(InvalidBody);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["model"], (await FieldErrorsAsync(response)).Keys);
        Assert.Empty(factory.Tester.Candidates);
    }

    [Fact]
    public async Task TheFourthTestWithinTheMinute_Returns429WithItsOwnDetailAndRetryAfter()
    {
        using var factory = NewFactory();
        var client = factory.CreateClient();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostTestAsync()).StatusCode);
        }

        var fourth = await client.PostTestAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.Equal(RateLimitDetail, await ProblemDetailAsync(fourth, 429));
        Assert.True(fourth.Headers.TryGetValues("Retry-After", out var values));
        Assert.InRange(int.Parse(Assert.Single(values), CultureInfo.InvariantCulture), 1, 60);
        Assert.Equal(3, factory.Tester.Candidates.Count);
    }

    [Fact]
    public async Task TheTestsThatEndIn400AlsoCountTowardsTheLimit()
    {
        using var factory = NewFactory();
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        foreach (var body in new[] { InvalidBody, InvalidBody, "{}", "{}" })
        {
            statuses.Add((await client.PostTestAsync(body)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task AnonymousAndEmployeeRequests_ConsumeNoPermits_SoTheManagerStillGetsThree()
    {
        using var factory = NewFactory();
        using var anonymous = factory.CreateAnonymousClient();
        using var employee = factory.CreateClientFor(factory.ManagerId, "Employee");

        var anonymousStatuses = new List<HttpStatusCode>();
        var employeeStatuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            anonymousStatuses.Add((await anonymous.PostTestAsync()).StatusCode);
            employeeStatuses.Add((await employee.PostTestAsync()).StatusCode);
        }

        Assert.All(anonymousStatuses, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.All(employeeStatuses, status => Assert.Equal(HttpStatusCode.Forbidden, status));
        var manager = factory.CreateClient();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await manager.PostTestAsync()).StatusCode);
        }
    }

    [Fact]
    public async Task TheLimitIsPerManager_AndTheOtherEndpointsOfTheGroupAreNotLimited()
    {
        using var factory = NewFactory();
        var client = factory.CreateClient();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await client.PostTestAsync();
        }

        var otherManager = await factory.CreateClientFor(Guid.NewGuid(), "Manager").PostTestAsync();
        var get = await client.GetAsync(AssistantConfigurationHttp.Route, CancellationToken.None);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (otherManager.StatusCode, get.StatusCode));
    }
}
