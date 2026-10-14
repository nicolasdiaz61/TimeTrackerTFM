using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Authentication;

[Collection("Postgres")]
public class LoginEndpointTests(PostgresFixture postgres)
{
    private const string LoginPath = "/api/auth/login";

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokenAndUser()
    {
        var user = await AddUserAsync("luis");
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await PostLoginAsync(factory.CreateAnonymousClient(), user.Email, UserSeed.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("accessToken").GetString()));
        // Se lee el JSON crudo: un DateTimeOffset se escribiría con +00:00 en lugar de Z.
        Assert.Equal("2026-10-12T08:30:00Z", body.RootElement.GetProperty("expiresAt").GetString());
        var userBody = body.RootElement.GetProperty("user");
        Assert.Equal(user.Id, userBody.GetProperty("id").GetGuid());
        Assert.Equal(user.Email, userBody.GetProperty("email").GetString());
        Assert.Equal("Employee", userBody.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_NormalizesEmail()
    {
        var user = await AddUserAsync("luis");
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await PostLoginAsync(factory.CreateAnonymousClient(), $"  {user.Email.ToUpperInvariant()}  ", UserSeed.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(user.Email, body.RootElement.GetProperty("user").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_Response_HasNoHashOrPassword()
    {
        var user = await AddUserAsync("luis");
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await PostLoginAsync(factory.CreateAnonymousClient(), user.Email, UserSeed.Password);

        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.PasswordHash, text, StringComparison.Ordinal);
        Assert.DoesNotContain(UserSeed.Password, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401ProblemJson()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await PostLoginAsync(factory.CreateAnonymousClient(), "nadie@example.test", UserSeed.Password);

        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401ProblemJson()
    {
        var user = await AddUserAsync("luis");
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await PostLoginAsync(factory.CreateAnonymousClient(), user.Email, "otra-clave-456");

        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_ProduceIdenticalResponses()
    {
        var user = await AddUserAsync("luis");
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();

        var unknown = await PostLoginAsync(client, "nadie@example.test", UserSeed.Password);
        var wrong = await PostLoginAsync(client, user.Email, "otra-clave-456");

        Assert.Equal(unknown.StatusCode, wrong.StatusCode);
        Assert.Equal(unknown.Content.Headers.ContentType?.MediaType, wrong.Content.Headers.ContentType?.MediaType);
        Assert.Equal(await BodyWithoutTraceIdAsync(unknown), await BodyWithoutTraceIdAsync(wrong));
    }

    [Theory]
    [InlineData("   ", "clave-de-prueba-123")]
    [InlineData("luis@example.test", null)]
    [InlineData(null, "clave-de-prueba-123")]
    public async Task Login_WithBlankOrMissingField_Returns401LikeWrongCredentials(string? email, string? password)
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await PostLoginAsync(factory.CreateAnonymousClient(), email, password);

        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Login_WithOversizedEmailOrPassword_Returns401LikeWrongCredentials()
    {
        var user = await AddUserAsync("luis");
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();

        await AssertGenericUnauthorizedAsync(await PostLoginAsync(client, new string('a', 255), UserSeed.Password));
        await AssertGenericUnauthorizedAsync(await PostLoginAsync(client, user.Email, new string('a', 129)));
    }

    [Fact]
    public async Task Login_WithUnreadableStoredHash_Returns401NotServerError()
    {
        var email = $"roto.{Guid.NewGuid():N}@example.test";
        await UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), email, passwordHash: "no-es-un-hash");
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await PostLoginAsync(factory.CreateAnonymousClient(), email, UserSeed.Password);

        await AssertGenericUnauthorizedAsync(response);
    }

    public static TheoryData<string, string> BrokenBodies => new()
    {
        { "Development", "none" }, { "Development", "empty-json" }, { "Development", "broken-json" },
        { "Production", "none" }, { "Production", "empty-json" }, { "Production", "broken-json" },
    };

    [Theory]
    [MemberData(nameof(BrokenBodies))]
    public async Task Login_WithoutBodyOrBrokenJson_Returns400ProblemJson(string environment, string kind)
    {
        // Development lanza BadHttpRequestException al enlazar; Production responde 400 sin lanzar: ambos deben dar lo mismo (V18).
        using var baseFactory = new PostgresApiFactory(postgres.ConnectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

        var response = await factory.CreateAnonymousClient().PostAsync(LoginPath, BodyFor(kind), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain("accessToken", text, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(text);
        Assert.Equal("Solicitud no válida", body.RootElement.GetProperty("title").GetString());
    }

    private static StringContent? BodyFor(string kind) => kind switch
    {
        "none" => null,
        "empty-json" => new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"),
        _ => new StringContent("{\"email\":", System.Text.Encoding.UTF8, "application/json"),
    };

    internal static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string? email, string? password)
        => client.PostAsJsonAsync(LoginPath, new { email, password }, CancellationToken.None);

    private static async Task AssertGenericUnauthorizedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Credenciales no válidas", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("El correo electrónico o la contraseña no son correctos.", body.RootElement.GetProperty("detail").GetString());
        Assert.False(body.RootElement.TryGetProperty("accessToken", out _));
    }

    // «Idéntico» significa idéntico salvo traceId, que cambia en cada petición.
    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        return string.Join(",", body.RootElement.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .Select(property => $"{property.Name}={property.Value.GetRawText()}"));
    }

    private async Task<UserAccount> AddUserAsync(string name, UserRole role = UserRole.Employee)
    {
        var user = UserSeed.Create(name, role);
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        await UserSeed.AddAsync(context, user);
        return user;
    }
}
