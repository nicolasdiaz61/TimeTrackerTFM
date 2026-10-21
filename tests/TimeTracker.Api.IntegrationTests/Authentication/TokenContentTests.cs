using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Authentication;

/// <summary>
/// Contenido del token emitido y de las respuestas de sesión (AU «Compañía en el token…» y «Compañía y estado de contraseña en la
/// sesión»). Los casos con login usan PostgreSQL real; <c>me</c> se prueba sin base de datos porque todo sale de los claims.
/// </summary>
[Collection("Postgres")]
public class TokenContentTests(PostgresFixture postgres)
{
    private const string LoginPath = "/api/auth/login";
    private const string MePath = "/api/auth/me";

    [Fact]
    public async Task Login_TokenCarriesTheCompanyClaimsAndNoPasswordChangeMarker()
    {
        var companyId = await CompanySeed.InsertAsync(postgres.ConnectionString, "Acme Norte");
        var email = await AddUserAsync(companyId);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await LoginAsync(factory, email);

        var payload = Payload(body.RootElement.GetProperty("accessToken").GetString()!);
        Assert.Equal(companyId.ToString(), payload.GetProperty("cid").GetString());
        Assert.Equal("Acme Norte", payload.GetProperty("cname").GetString());
        Assert.False(payload.TryGetProperty("mcp", out _));
        var user = body.RootElement.GetProperty("user");
        Assert.Equal("Acme Norte", user.GetProperty("companyName").GetString());
        Assert.False(user.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Login_WithAPendingPasswordChange_AddsMcpToTheTokenAndReportsItInTheUser()
    {
        var companyId = await CompanySeed.InsertAsync(postgres.ConnectionString, "Acme Sur");
        var email = await AddUserAsync(companyId, mustChangePassword: true);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await LoginAsync(factory, email);

        Assert.Equal("true", Payload(body.RootElement.GetProperty("accessToken").GetString()!).GetProperty("mcp").GetString());
        Assert.True(body.RootElement.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Login_ResponseNeverContainsACompanyIdentifier()
    {
        var companyId = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var email = await AddUserAsync(companyId);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await PostLoginAsync(factory, email);

        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("companyId", text, StringComparison.OrdinalIgnoreCase);
        // El identificador solo viaja dentro del token (claim cid), nunca como dato de la respuesta: el cuerpo sin el token no lo contiene.
        using var body = JsonDocument.Parse(text);
        Assert.DoesNotContain(companyId.ToString(), body.RootElement.GetProperty("user").GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WithASuspendedCompany_Returns401LikeWrongCredentialsAndIssuesNoToken()
    {
        var companyId = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var email = await AddUserAsync(companyId);
        await SuspendAsync(companyId);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var suspended = await PostLoginAsync(factory, email);
        var wrong = await PostLoginAsync(factory, email, "otra-clave-456");

        Assert.Equal(HttpStatusCode.Unauthorized, suspended.StatusCode);
        Assert.Equal(await BodyWithoutTraceIdAsync(wrong), await BodyWithoutTraceIdAsync(suspended));
        Assert.DoesNotContain("accessToken", await suspended.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_ReturnsTheCompanyNameAndPendingChangeFromTheClaimsWithoutQueryingTheDatabase()
    {
        // La fábrica base apunta a db.invalid: si /me tocara la base, fallaría con 500.
        using var factory = new ApiFactory();

        using var withoutMarker = await MeAsync(factory, TestTokens.Create(Guid.NewGuid(), "Employee", factory.Clock.GetUtcNow(), companyName: "Acme Norte"));
        using var withMarker = await MeAsync(factory, TestTokens.Create(Guid.NewGuid(), "Employee", factory.Clock.GetUtcNow(), companyName: "Acme Sur", passwordChangePending: "true"));

        Assert.Equal(("Acme Norte", false), (withoutMarker.RootElement.GetProperty("companyName").GetString(), withoutMarker.RootElement.GetProperty("mustChangePassword").GetBoolean()));
        Assert.Equal(("Acme Sur", true), (withMarker.RootElement.GetProperty("companyName").GetString(), withMarker.RootElement.GetProperty("mustChangePassword").GetBoolean()));
    }

    [Fact]
    public async Task Me_WithAnMcpThatIsNotTrue_ReportsNoPendingChange()
    {
        using var factory = new ApiFactory();

        using var body = await MeAsync(factory, TestTokens.Create(Guid.NewGuid(), "Employee", factory.Clock.GetUtcNow(), passwordChangePending: "false"));

        Assert.False(body.RootElement.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Me_NeverExposesACompanyIdentifier()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var text = await client.GetStringAsync(MePath, CancellationToken.None);

        Assert.DoesNotContain("companyId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestTokens.DefaultCompanyId, text, StringComparison.Ordinal);
    }

    private async Task<string> AddUserAsync(Guid companyId, bool mustChangePassword = false)
    {
        var user = UserAccount.Create(Guid.NewGuid(), companyId, $"ana.{Guid.NewGuid():N}@example.test", "Ana Ejemplo", UserSeed.PasswordHash, UserRole.Manager, null, mustChangePassword);
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        await UserSeed.AddAsync(context, user);
        return user.Email;
    }

    private async Task SuspendAsync(Guid companyId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand("UPDATE companies SET status = 'Suspended' WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", companyId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(CancellationToken.None));
    }

    private static Task<HttpResponseMessage> PostLoginAsync(ApiFactory factory, string email, string password = UserSeed.Password)
        => factory.CreateAnonymousClient().PostAsJsonAsync(LoginPath, new { email, password }, CancellationToken.None);

    private static async Task<JsonDocument> LoginAsync(ApiFactory factory, string email)
    {
        var response = await PostLoginAsync(factory, email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    private static async Task<JsonDocument> MeAsync(ApiFactory factory, string token)
    {
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync(MePath, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    // Se decodifica a mano (base64url + JSON): la prueba no depende de la biblioteca que emite el token.
    private static JsonElement Payload(string token)
    {
        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(token.Split('.')[1]));
        return payload.RootElement.Clone();
    }

    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        return string.Join(",", body.RootElement.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .Select(property => $"{property.Name}={property.Value.GetRawText()}"));
    }
}
