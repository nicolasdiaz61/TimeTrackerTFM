using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using TimeTracker.Infrastructure.Tests.Fakes;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// Cada prueba crea su propia fábrica: los contadores del limitador y de la cuota viven en el host.
/// Sin dirección remota (TestServer) todos los clientes comparten IP: un aislamiento entre usuarios prueba que se particiona por <c>sub</c>.
/// </summary>
public class AssistantRateLimitTests
{
    private const string MinuteDetail = "Ha realizado demasiadas consultas al asistente en poco tiempo. Espere un minuto antes de volver a intentarlo.";
    private const string DailyDetail = "Ha alcanzado el límite diario de consultas al asistente. Inténtelo de nuevo mañana.";

    private static FakeChatClient Model() => new(Enumerable.Repeat(Reply("ok"), 20).ToArray());

    private static int RetryAfter(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        return int.Parse(Assert.Single(values), CultureInfo.InvariantCulture);
    }

    private static async Task<HttpResponseMessage[]> SendAsync(HttpClient client, int count, string message = "hola")
    {
        var responses = new List<HttpResponseMessage>();
        for (var attempt = 0; attempt < count; attempt++)
        {
            responses.Add(await client.AskAsync(message));
        }

        return [.. responses];
    }

    [Fact]
    public async Task SixthRequestWithinTheMinute_Returns429WithRetryAfterAndTheModelIsNotInvoked()
    {
        var model = Model();
        using var factory = new AssistantApiFactory(model);
        var client = factory.CreateClient();
        await SendAsync(client, 5);

        var sixth = await client.AskAsync("hola");

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.InRange(RetryAfter(sixth), 1, 60);
        Assert.Equal(MinuteDetail, await ProblemDetailAsync(sixth, 429));
        Assert.Equal(5, model.Calls.Count);
    }

    [Fact]
    public async Task FiveRequests_AreAllAllowed()
    {
        using var factory = new AssistantApiFactory(Model());

        var responses = await SendAsync(factory.CreateClient(), 5);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public async Task RequestsPerMinute_IsConfigurable()
    {
        using var factory = new AssistantApiFactory(Model(), new Dictionary<string, string?> { ["Ai:RequestsPerMinute"] = "2" });
        var client = factory.CreateClient();

        var responses = await SendAsync(client, 3);

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], responses.Select(response => response.StatusCode));
    }

    [Fact]
    public async Task Users_AreIndependentEvenFromTheSameAddress()
    {
        using var factory = new AssistantApiFactory(Model());
        var luis = factory.CreateClientFor(Guid.NewGuid(), "Employee");
        var ana = factory.CreateClientFor(Guid.NewGuid(), "Employee");
        await SendAsync(luis, 5);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await luis.AskAsync("hola")).StatusCode);

        var response = await ana.AskAsync("hola");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RateLimited429_KeepsCorsAndSecurityHeaders()
    {
        using var factory = new AssistantApiFactory(Model());
        var client = factory.CreateClient();
        await SendAsync(client, 5);
        var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(new { message = "hola" }) };
        request.Headers.Add("Origin", ApiFactory.ValidOrigin);

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal([ApiFactory.ValidOrigin], response.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("default-src 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidRequestsAndDisabledAssistant_CountForTheMinuteButNotForTheDay()
    {
        using var factory = new AssistantApiFactory(settings: new Dictionary<string, string?> { ["Ai:RequestsPerMinute"] = "3" });
        var client = factory.CreateClient();

        var responses = await SendAsync(client, 4);

        Assert.Equal(
            [HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.TooManyRequests],
            responses.Select(response => response.StatusCode));
    }

    [Fact]
    public async Task DailyQuota_ThirdRequestWithLimitTwo_Returns429WithRetryAfterUntilLocalMidnight()
    {
        var model = Model();
        using var factory = new AssistantApiFactory(model, new Dictionary<string, string?> { ["Ai:DailyRequestsPerUser"] = "2" });
        var client = factory.CreateClient();
        await SendAsync(client, 2);

        var third = await client.AskAsync("hola");

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        // 2026-10-12T08:00Z son las 10:00 en España (UTC+2): faltan 14 h hasta la medianoche local.
        Assert.Equal(14 * 3600, RetryAfter(third));
        Assert.Equal(DailyDetail, await ProblemDetailAsync(third, 429));
        Assert.Equal(2, model.Calls.Count);
    }

    [Fact]
    public async Task DailyQuota_ResetsAtLocalMidnight()
    {
        using var factory = new AssistantApiFactory(Model(), new Dictionary<string, string?> { ["Ai:DailyRequestsPerUser"] = "1" });
        var client = factory.CreateClient();
        await client.AskAsync("hola");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.AskAsync("hola")).StatusCode);

        // Pasada la medianoche local (22:00 UTC del 12 son las 00:00 del 13 en España) el contador se reinicia.
        factory.Clock.Advance(TimeSpan.FromHours(14));
        var response = await factory.CreateClientFor(ApiFactory.DefaultUserId, "Employee").AskAsync("hola");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DailyQuota_IsPerUser()
    {
        using var factory = new AssistantApiFactory(Model(), new Dictionary<string, string?> { ["Ai:DailyRequestsPerUser"] = "1" });
        var luis = factory.CreateClientFor(Guid.NewGuid(), "Employee");
        var ana = factory.CreateClientFor(Guid.NewGuid(), "Employee");
        await luis.AskAsync("hola");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await luis.AskAsync("hola")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await ana.AskAsync("hola")).StatusCode);
    }

    [Fact]
    public async Task InvalidInputs_DoNotConsumeTheDailyQuota()
    {
        using var factory = new AssistantApiFactory(Model(), new Dictionary<string, string?> { ["Ai:DailyRequestsPerUser"] = "2" });
        var client = factory.CreateClient();

        await SendAsync(client, 2, message: "");
        var valid = await client.AskAsync("hola");

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task AnonymousRequests_DoNotConsumeAnyUsersQuota()
    {
        using var factory = new AssistantApiFactory(Model());
        var anonymous = await SendAsync(factory.CreateAnonymousClient(), 12);

        var first = await factory.CreateClient().AskAsync("hola");

        Assert.All(anonymous, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    }

    [Fact]
    public async Task Preflight_NeedsNoTokenAndConsumesNoQuota()
    {
        using var factory = new AssistantApiFactory(Model());
        var anonymous = factory.CreateAnonymousClient();

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var preflight = new HttpRequestMessage(HttpMethod.Options, Route);
            preflight.Headers.Add("Origin", ApiFactory.ValidOrigin);
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
            var response = await anonymous.SendAsync(preflight, CancellationToken.None);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal([ApiFactory.ValidOrigin], response.Headers.GetValues("Access-Control-Allow-Origin"));
        }

        var allowed = await SendAsync(factory.CreateClient(), 5);
        Assert.All(allowed, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public async Task AssistantLimitDoesNotCountLoginsAndLoginLimitDoesNotCountAssistantRequests()
    {
        using var factory = new AssistantApiFactory(Model());
        var client = factory.CreateClient();
        var anonymous = factory.CreateAnonymousClient();

        // Correo en blanco: 401 sin consultar la base; lo que se prueba es el contador.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Authentication.LoginEndpointTests.PostLoginAsync(anonymous, "   ", "x")).StatusCode);
        }

        await SendAsync(client, 5);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.AskAsync("hola")).StatusCode);
        var login = await Authentication.LoginEndpointTests.PostLoginAsync(anonymous, "   ", "x");

        // El minuto del asistente agotado no cuenta como login, y los diez logins no cuentan para el asistente: el undécimo login sí da 429, con su propio texto.
        Assert.Equal(HttpStatusCode.TooManyRequests, login.StatusCode);
        Assert.Equal("Espere un minuto antes de volver a intentarlo.", await ProblemDetailAsync(login, 429));
    }

    [Fact]
    public async Task AssistantRequestsAlone_DoNotLimitLogin()
    {
        using var factory = new AssistantApiFactory(Model());
        await SendAsync(factory.CreateClient(), 6);

        var login = await Authentication.LoginEndpointTests.PostLoginAsync(factory.CreateAnonymousClient(), "   ", "x");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
