using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TimeTracker.Api.Security;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// Política <c>assistant-config-test</c> (DD-15) con el registro real de <c>AddRateLimiting()</c> y el mismo orden que <c>Program</c>
/// (autenticación, autorización, limitador) sobre dos rutas mínimas: el endpoint real llega en la PR 6b, que repite la comprobación
/// sobre él. La identidad sale de las cabeceras <c>X-Sub</c> y <c>X-Role</c>; sin ellas la petición es anónima.
/// </summary>
public sealed class AssistantConfigTestRateLimitTests
{
    private const string ConfigTestRoute = "/prueba";
    private const string ChatRoute = "/chat";
    private const string ConfigTestDetail = "Ha realizado demasiadas pruebas de conexión. Espere un minuto antes de volver a intentarlo.";
    private const string ChatDetail = "Ha realizado demasiadas consultas al asistente en poco tiempo. Espere un minuto antes de volver a intentarlo.";

    private sealed class HeaderAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Sub", out var sub))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim("sub", sub.ToString()), new Claim(ClaimTypes.Role, Request.Headers["X-Role"].ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private static async Task<WebApplication> StartHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddOptions<TimeTracker.Infrastructure.Assistant.AiOptions>();
        builder.Services.AddRateLimiting();
        builder.Services.AddAuthentication("Prueba").AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Prueba", null);
        builder.Services.AddAuthorizationBuilder().AddPolicy("Manager", policy => policy.RequireRole("Manager"));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapPost(ConfigTestRoute, () => Results.Ok()).RequireAuthorization("Manager").RequireRateLimiting(RateLimitingServiceCollectionExtensions.AssistantConfigTestPolicy);
        app.MapPost(ChatRoute, () => Results.Ok()).RequireAuthorization().RequireRateLimiting(RateLimitingServiceCollectionExtensions.AssistantPolicy);
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage[]> PostAsync(HttpClient client, string route, int count, string? sub = null, string role = "Manager")
    {
        var responses = new List<HttpResponseMessage>();
        for (var attempt = 0; attempt < count; attempt++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, route);
            if (sub is not null)
            {
                request.Headers.Add("X-Sub", sub);
                request.Headers.Add("X-Role", role);
            }

            responses.Add(await client.SendAsync(request));
        }

        return [.. responses];
    }

    private static IEnumerable<HttpStatusCode> Statuses(IEnumerable<HttpResponseMessage> responses) => responses.Select(response => response.StatusCode);

    [Fact]
    public async Task FourthTestWithinTheMinute_Returns429WithItsOwnDetailAndRetryAfter()
    {
        await using var app = await StartHostAsync();
        using var client = app.GetTestClient();
        await PostAsync(client, ConfigTestRoute, 3, "gerente-1");

        var fourth = Assert.Single(await PostAsync(client, ConfigTestRoute, 1, "gerente-1"));

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.Equal(ConfigTestDetail, await ProblemDetailAsync(fourth, 429));
        Assert.True(fourth.Headers.TryGetValues("Retry-After", out var values));
        Assert.InRange(int.Parse(Assert.Single(values), CultureInfo.InvariantCulture), 1, 60);
    }

    [Fact]
    public async Task TheFirstThreeTests_AreAllowed()
    {
        await using var app = await StartHostAsync();
        using var client = app.GetTestClient();

        var responses = await PostAsync(client, ConfigTestRoute, 3, "gerente-1");

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK], Statuses(responses));
    }

    [Fact]
    public async Task TheLimitIsPerSubClaim_EvenFromTheSameAddress()
    {
        await using var app = await StartHostAsync();
        using var client = app.GetTestClient();
        await PostAsync(client, ConfigTestRoute, 4, "gerente-1");

        var other = await PostAsync(client, ConfigTestRoute, 3, "gerente-2");

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK], Statuses(other));
    }

    [Fact]
    public async Task AnonymousAndEmployeeRequests_ConsumeNoPermitsBecauseThePolicyRunsAfterAuthorization()
    {
        await using var app = await StartHostAsync();
        using var client = app.GetTestClient();

        var anonymous = await PostAsync(client, ConfigTestRoute, 6);
        var employee = await PostAsync(client, ConfigTestRoute, 6, "gerente-1", role: "Employee");
        var manager = await PostAsync(client, ConfigTestRoute, 3, "gerente-1");

        Assert.All(anonymous, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.All(employee, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK], Statuses(manager));
    }

    [Fact]
    public async Task TheChatPolicyKeepsItsOwnLimitAndDetail_AndTheTwoCountersAreIndependent()
    {
        await using var app = await StartHostAsync();
        using var client = app.GetTestClient();
        await PostAsync(client, ConfigTestRoute, 3, "gerente-1");

        var chat = await PostAsync(client, ChatRoute, 6, "gerente-1");

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests],
            Statuses(chat));
        Assert.Equal(ChatDetail, await ProblemDetailAsync(chat[^1], 429));
        var configTest = Assert.Single(await PostAsync(client, ConfigTestRoute, 1, "gerente-1"));
        Assert.Equal(ConfigTestDetail, await ProblemDetailAsync(configTest, 429));
    }
}
