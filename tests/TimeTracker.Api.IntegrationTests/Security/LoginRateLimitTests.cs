using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;

using TimeTracker.Api.IntegrationTests.Authentication;
using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;

namespace TimeTracker.Api.IntegrationTests.Security;

/// <summary>Cada prueba crea su propia fábrica: los contadores del limitador viven en el host y no se comparten.</summary>
[Collection("Postgres")]
public class LoginRateLimitTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Login_EleventhAttemptWithinMinute_Returns429ProblemJsonWithRetryAfter()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        await SendWrongAttemptsAsync(client, 10);

        var response = await LoginEndpointTests.PostLoginAsync(client, "nadie@example.test", "otra-clave-456");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        using var body = await AccessErrorsTests.AssertProblemAsync(response, 429, "Demasiadas solicitudes");
        Assert.Equal("Espere un minuto antes de volver a intentarlo.", body.RootElement.GetProperty("detail").GetString());
        Assert.True(response.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.True(int.Parse(Assert.Single(retryAfter), System.Globalization.CultureInfo.InvariantCulture) > 0);
    }

    [Fact]
    public async Task Login_TenWrongAttempts_NoneReturns429()
    {
        var user = await AddUserAsync();
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await LoginEndpointTests.PostLoginAsync(client, user.Email, "otra-clave-456");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Login_SuccessfulAttemptsAlsoCount()
    {
        var user = await AddUserAsync();
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await LoginEndpointTests.PostLoginAsync(client, user.Email, UserSeed.Password);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var eleventh = await LoginEndpointTests.PostLoginAsync(client, user.Email, UserSeed.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Fact]
    public async Task OtherRoutes_AreNotLimited()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var anonymous = factory.CreateAnonymousClient();
        await SendWrongAttemptsAsync(anonymous, 10);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginEndpointTests.PostLoginAsync(anonymous, "nadie@example.test", "x")).StatusCode);
        var authenticated = factory.CreateClient();

        for (var request = 0; request < 20; request++)
        {
            var response = await authenticated.GetAsync("/api/auth/me", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task RateLimited429_HasCorsOrigin()
    {
        const string webOrigin = "http://localhost:5172";
        using var baseFactory = new PostgresApiFactory(postgres.ConnectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", webOrigin));
        var client = factory.CreateAnonymousClient();
        await SendWrongAttemptsAsync(client, 10);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "nadie@example.test", password = "x" }),
        };
        request.Headers.Add("Origin", webOrigin);

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal([webOrigin], response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    private static async Task SendWrongAttemptsAsync(HttpClient client, int count)
    {
        for (var attempt = 0; attempt < count; attempt++)
        {
            // Correo en blanco: 401 sin consultar la base; lo que se prueba aquí es el contador, no las credenciales.
            var response = await LoginEndpointTests.PostLoginAsync(client, "   ", "x");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private async Task<TimeTracker.Domain.Users.UserAccount> AddUserAsync()
    {
        var user = UserSeed.Create("luis");
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        await UserSeed.AddAsync(context, user);
        return user;
    }
}
