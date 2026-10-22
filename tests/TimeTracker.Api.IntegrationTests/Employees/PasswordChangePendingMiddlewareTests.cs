using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// TC-16 (DD-21): mientras el token lleva <c>mcp</c> igual a <c>"true"</c> (valor estricto, no mera presencia) solo se permiten
/// <c>GET /api/auth/me</c> y <c>POST /api/auth/change-password</c>. La decisión sale solo del claim: la fábrica sin base de datos
/// de las primeras pruebas fallaría con un 500 ante cualquier consulta.
/// </summary>
[Collection("Postgres")]
public class PasswordChangePendingMiddlewareTests(PostgresFixture postgres)
{
    private const string Pending = "true";

    private static HttpClient ClientWith(ApiFactory factory, string role, string? mcp)
    {
        var client = factory.CreateAnonymousClient();
        var token = TestTokens.Create(Guid.NewGuid(), role, factory.Clock.GetUtcNow(), passwordChangePending: mcp);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task WithAPendingChange_TimeEntriesTeamAndTheAssistantAreForbiddenAsProblemJson_WithoutTouchingTheDatabase()
    {
        using var factory = new ApiFactory();
        using var employee = ClientWith(factory, "Employee", Pending);
        using var manager = ClientWith(factory, "Manager", Pending);

        var responses = new[]
        {
            await employee.GetAsync("/api/time-entries", CancellationToken.None),
            await manager.GetAsync("/api/team/members", CancellationToken.None),
            await employee.PostAsync("/api/asistente", Json("""{"message":"hola"}"""), CancellationToken.None),
            await employee.PostAsync("/api/time-entries/clock-in", null, CancellationToken.None),
        };

        Assert.All(responses, response =>
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        });
        using var body = JsonDocument.Parse(await responses[0].Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal("Acceso denegado", body.RootElement.GetProperty("title").GetString());
        Assert.Contains("cambiar su contraseña", body.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAPendingChange_TheSameEndpointsAreServed()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString, fixture.A.Manager.Id, "Manager");
        var manager = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/time-entries", CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/team/members", CancellationToken.None)).StatusCode);
        using var assistantFactory = new ApiFactory();
        using var employee = ClientWith(assistantFactory, "Employee", mcp: null);
        // El asistente del entorno de prueba está deshabilitado: 503, nunca 403.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await employee.PostAsync("/api/asistente", Json("""{"message":"hola"}"""), CancellationToken.None)).StatusCode);
    }

    [Theory]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("false")]
    [InlineData("")]
    [InlineData(" true")]
    public async Task AnMcpOtherThanExactlyTrue_IsTreatedAsNoPendingChange(string mcp)
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString, fixture.A.Manager.Id, "Manager");
        var client = factory.CreateAnonymousClient();
        var token = TestTokens.Create(fixture.A.Manager.Id, "Manager", factory.Clock.GetUtcNow(), companyId: fixture.A.Id.ToString(), companyName: fixture.A.Name, passwordChangePending: mcp);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/team/members", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", CancellationToken.None);
        Assert.False(me.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Me_IsNotForbiddenByThePendingChange_AndReportsIt()
    {
        using var factory = new ApiFactory();
        using var client = ClientWith(factory, "Employee", Pending);

        var me = await client.GetAsync("/api/auth/me", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meBody = JsonDocument.Parse(await me.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.True(meBody.RootElement.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task AnonymousEndpoints_AreNotBlockedByAStaleTokenWithAPendingChange()
    {
        using var factory = new ApiFactory();
        using var client = ClientWith(factory, "Employee", Pending);

        var health = await client.GetAsync("/health", CancellationToken.None);
        var login = await client.PostAsync("/api/auth/login", Json("""{"email":"","password":""}"""), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task AnUnknownRoute_WithAPendingChange_IsForbiddenToo_AndWithoutOneIsNotFound()
    {
        using var factory = new ApiFactory();
        using var pending = ClientWith(factory, "Employee", Pending);
        using var normal = ClientWith(factory, "Employee", mcp: null);

        Assert.Equal(HttpStatusCode.Forbidden, (await pending.GetAsync("/ruta-que-no-existe", CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await normal.GetAsync("/ruta-que-no-existe", CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task AnAnonymousRequestToAProtectedEndpoint_Gets401NotTheForbiddenOfThePendingChange()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/time-entries", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheMiddleware_RunsAfterAuthenticationAndBeforeAuthorization()
    {
        var probe = new Probe();
        using var factory = new ProbedFactory(probe);
        using var pending = ClientWith(factory, "Employee", Pending);
        using var normal = ClientWith(factory, "Employee", mcp: null);

        var blocked = await pending.GetAsync("/api/time-entries", CancellationToken.None);
        var blockedEvaluations = probe.Evaluations;
        await normal.GetAsync("/api/auth/me", CancellationToken.None);

        // Si corriera después de la autorización, el manejador de la política de respaldo ya se habría ejecutado.
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(0, blockedEvaluations);
        // Control: el mismo manejador sí se ejecuta cuando la petición llega a la autorización.
        Assert.Equal(1, probe.Evaluations);
    }

    private sealed class Probe
    {
        private int _evaluations;

        public int Evaluations => Volatile.Read(ref _evaluations);

        public void Evaluated() => Interlocked.Increment(ref _evaluations);
    }

    private sealed class ProbedFactory(Probe probe) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(probe);
                services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, ProbeHandler>());
            });
        }
    }

    private sealed class ProbeHandler(Probe probe) : AuthorizationHandler<DenyAnonymousAuthorizationRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DenyAnonymousAuthorizationRequirement requirement)
        {
            probe.Evaluated();
            return Task.CompletedTask;
        }
    }
}
