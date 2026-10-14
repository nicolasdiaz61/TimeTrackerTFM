using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Api.IntegrationTests.Security;

[Collection("Postgres")]
public class AccessErrorsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Unauthorized_WithoutToken_IsProblemJsonInSpanishWithoutDetail()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/api/time-entries", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = await AssertProblemAsync(response, 401, "Se requiere autenticación");
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthorized_WithExpiredToken_OnTimeEntries_IsProblemJson()
    {
        using var factory = new ApiFactory();
        var expired = TestTokens.Create(ApiFactory.DefaultUserId, "Employee", factory.Clock.GetUtcNow(), lifetime: TimeSpan.FromMinutes(-5));
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", expired);

        var response = await client.GetAsync("/api/time-entries", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = await AssertProblemAsync(response, 401, "Se requiere autenticación");
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task NotFound_WithToken_IsProblemJson()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync("/ruta-que-no-existe", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = await AssertProblemAsync(response, 404, "Recurso no encontrado");
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task MethodNotAllowed_WithToken_IsProblemJson()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsync("/health", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        using var body = await AssertProblemAsync(response, 405, "Método no permitido");
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task AccessErrorBodies_HaveNoTraces()
    {
        using var factory = new ApiFactory();
        var anonymous = await factory.CreateAnonymousClient().GetAsync("/api/time-entries", CancellationToken.None);
        var missing = await factory.CreateClient().GetAsync("/ruta-que-no-existe", CancellationToken.None);

        foreach (var response in new[] { anonymous, missing })
        {
            var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
            Assert.DoesNotContain("Exception", text, StringComparison.Ordinal);
            Assert.DoesNotContain("at TimeTracker", text, StringComparison.Ordinal);
            Assert.DoesNotContain("stackTrace", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // --- Regresión de V9: StatusCodePages no pisa los cuerpos que la API ya escribe ---

    [Fact]
    public async Task LoginUnauthorized_KeepsItsDetail()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await Authentication.LoginEndpointTests.PostLoginAsync(factory.CreateAnonymousClient(), "nadie@example.test", "clave-de-prueba-123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal("Credenciales no válidas", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("El correo electrónico o la contraseña no son correctos.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Conflict409_KeepsItsBody()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        var response = await client.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        Assert.True(body.RootElement.TryGetProperty("detail", out var detail) && !string.IsNullOrWhiteSpace(detail.GetString()));
    }

    [Fact]
    public async Task UnhandledException_KeepsItsTitle()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.Replace(ServiceDescriptor.Scoped<ITimeEntryRepository, ThrowingRepository>())));

        var response = await factory.CreateClient().PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var body = await AssertProblemAsync(response, 500, "Se produjo un error inesperado");
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
    }

    internal static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, int status, string title)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(title, body.RootElement.GetProperty("title").GetString());
        return body;
    }

    private sealed class ThrowingRepository : ITimeEntryRepository
    {
        public Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");

        public Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");

        public Task AddAsync(TimeEntry entry, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");

        public Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");

        public Task ReplaceOpenAsync(TimeEntry closed, TimeEntry next, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");
    }
}
