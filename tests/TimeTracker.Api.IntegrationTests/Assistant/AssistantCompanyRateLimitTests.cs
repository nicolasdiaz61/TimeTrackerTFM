using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using TimeTracker.Infrastructure.Tests.Fakes;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// assistant-endpoint, «Límite de peticiones por minuto por compañía» y «Tope diario por compañía». Valores pequeños de configuración
/// y reloj falso; varias personas de la misma compañía comparten IP (TestServer), así que se prueba la partición por usuario y por compañía.
/// </summary>
public class AssistantCompanyRateLimitTests
{
    private const string CompanyMinuteDetail = "Su compañía ha superado el límite de consultas por minuto. Inténtelo de nuevo en unos segundos.";
    private const string CompanyDailyDetail = "Su compañía ha alcanzado el tope diario de consultas del asistente.";

    private static FakeChatClient Model() => new(Enumerable.Repeat(Reply("ok"), 40).ToArray());

    private static Dictionary<string, string?> Settings(params (string Key, string Value)[] values)
        => values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);

    private static int RetryAfter(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        return int.Parse(Assert.Single(values), CultureInfo.InvariantCulture);
    }

    private static HttpClient NewUser(AssistantApiFactory factory) => factory.CreateClientFor(Guid.NewGuid(), "Employee");

    [Fact]
    public async Task TheFourthRequestOfAnotherUserWithinTheMinute_Returns429WithTheCompanyDetail_AndTheModelIsNotInvoked()
    {
        var model = Model();
        using var factory = new AssistantApiFactory(model, Settings(("Ai:RequestsPerMinutePerCompany", "3")));
        for (var user = 0; user < 3; user++)
        {
            Assert.Equal(HttpStatusCode.OK, (await NewUser(factory).AskAsync("hola")).StatusCode);
        }

        var fourth = await NewUser(factory).AskAsync("hola");

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.InRange(RetryAfter(fourth), 1, 60);
        Assert.Equal(CompanyMinuteDetail, await ProblemDetailAsync(fourth, 429));
        Assert.Equal(3, model.Calls.Count);
    }

    [Fact]
    public async Task TheCompanyMinuteWindow_ResetsAfterAMinute()
    {
        using var factory = new AssistantApiFactory(Model(), Settings(("Ai:RequestsPerMinutePerCompany", "1")));
        Assert.Equal(HttpStatusCode.OK, (await NewUser(factory).AskAsync("hola")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await NewUser(factory).AskAsync("hola")).StatusCode);

        factory.Clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal(HttpStatusCode.OK, (await NewUser(factory).AskAsync("hola")).StatusCode);
    }

    [Fact]
    public async Task ARejectionByTheUserLimit_DoesNotConsumeTheCompanyMinute()
    {
        using var factory = new AssistantApiFactory(Model(), Settings(("Ai:RequestsPerMinute", "1"), ("Ai:RequestsPerMinutePerCompany", "2")));
        var first = NewUser(factory);
        Assert.Equal(HttpStatusCode.OK, (await first.AskAsync("hola")).StatusCode);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, (await first.AskAsync("hola")).StatusCode);
        }

        var second = await NewUser(factory).AskAsync("hola");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task AnonymousRequests_DoNotConsumeTheCompanyMinute()
    {
        using var factory = new AssistantApiFactory(Model(), Settings(("Ai:RequestsPerMinutePerCompany", "1")));
        var anonymous = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => factory.CreateAnonymousClient().AskAsync("hola")));

        var first = await NewUser(factory).AskAsync("hola");

        Assert.All(anonymous, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedRequestsRejectedLaterWith400Or503_DoCountForTheCompanyMinute()
    {
        using var disabled = new AssistantApiFactory(settings: Settings(("Ai:RequestsPerMinutePerCompany", "2")));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await NewUser(disabled).AskAsync("hola")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await NewUser(disabled).AskAsync("hola")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await NewUser(disabled).AskAsync("hola")).StatusCode);

        using var enabled = new AssistantApiFactory(Model(), Settings(("Ai:RequestsPerMinutePerCompany", "2")));
        Assert.Equal(HttpStatusCode.BadRequest, (await NewUser(enabled).AskAsync("")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await NewUser(enabled).AskAsync("")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await NewUser(enabled).AskAsync("hola")).StatusCode);
    }

    [Fact]
    public async Task TheCompanyMinute429_KeepsCorsAndSecurityHeaders()
    {
        using var factory = new AssistantApiFactory(Model(), Settings(("Ai:RequestsPerMinutePerCompany", "1")));
        await NewUser(factory).AskAsync("hola");
        var client = NewUser(factory);
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
    public async Task TheDailyCompanyCap_RejectsTheNextUserWithItsOwnDetailAndRetryAfterUntilMidnight()
    {
        var model = Model();
        using var factory = new AssistantApiFactory(model, Settings(("Ai:DailyRequestsPerCompany", "3")));
        for (var user = 0; user < 3; user++)
        {
            Assert.Equal(HttpStatusCode.OK, (await NewUser(factory).AskAsync("hola")).StatusCode);
        }

        var fourth = await NewUser(factory).AskAsync("hola");

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        // 2026-10-12T08:00Z son las 10:00 en España: faltan 14 h hasta la medianoche local.
        Assert.Equal(14 * 3600, RetryAfter(fourth));
        Assert.Equal(CompanyDailyDetail, await ProblemDetailAsync(fourth, 429));
        Assert.Equal(3, model.Calls.Count);
    }

    [Fact]
    public async Task TheDailyCompanyCap_ResetsAtLocalMidnight_AndInvalidInputsDoNotCount()
    {
        using var factory = new AssistantApiFactory(Model(), Settings(("Ai:DailyRequestsPerCompany", "2")));
        Assert.Equal(HttpStatusCode.BadRequest, (await NewUser(factory).AskAsync("")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await NewUser(factory).AskAsync("")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await NewUser(factory).AskAsync("hola")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await NewUser(factory).AskAsync("hola")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await NewUser(factory).AskAsync("hola")).StatusCode);

        factory.Clock.Advance(TimeSpan.FromHours(14));

        Assert.Equal(HttpStatusCode.OK, (await NewUser(factory).AskAsync("hola")).StatusCode);
    }
}
