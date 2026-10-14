using System.Net;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;

namespace TimeTracker.Api.IntegrationTests.Authentication;

/// <summary>Cada token se contrasta con un control válido (<c>/api/auth/me</c> responde 200) que difiere en una sola cosa.</summary>
[Collection("Postgres")]
public class TokenValidationTests(PostgresFixture postgres)
{
    private static readonly Guid UserId = Guid.Parse("0e6b1b7e-2f0b-4c1e-9d5f-6a1c3d9b7e21");

    [Fact]
    public async Task Token_Valid_IsAccepted()
    {
        using var factory = new ApiFactory();

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, Token(factory)));
    }

    [Fact]
    public async Task Token_FromLogin_IsAcceptedByMe()
    {
        var user = UserSeed.Create("luis");
        await using (var context = postgres.CreateContext(postgres.ConnectionString))
        {
            await UserSeed.AddAsync(context, user);
        }

        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var login = await LoginEndpointTests.PostLoginAsync(factory.CreateAnonymousClient(), user.Email, UserSeed.Password);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync(CancellationToken.None));

        // El reloj de la aplicación está en 2026-10-12: con el reloj del sistema el token ya habría caducado (V6).
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, body.RootElement.GetProperty("accessToken").GetString()!));
    }

    [Fact]
    public async Task Token_ExpiredTwoMinutesAgo_Returns401()
    {
        using var factory = new ApiFactory();
        var now = factory.Clock.GetUtcNow();

        var token = Token(factory, issuedAt: now - TimeSpan.FromHours(1), expires: now - TimeSpan.FromMinutes(2));

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_NotBeforeFiveMinutesAhead_Returns401()
    {
        using var factory = new ApiFactory();
        var now = factory.Clock.GetUtcNow();

        var token = Token(factory, notBefore: now + TimeSpan.FromMinutes(5));

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_ExpiredTenSecondsAgo_Returns200()
    {
        using var factory = new ApiFactory();
        var now = factory.Clock.GetUtcNow();

        var token = Token(factory, issuedAt: now - TimeSpan.FromHours(1), expires: now - TimeSpan.FromSeconds(10));

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_FromLogin_AfterClockAdvance31Minutes_Returns401()
    {
        var user = UserSeed.Create("luis");
        await using (var context = postgres.CreateContext(postgres.ConnectionString))
        {
            await UserSeed.AddAsync(context, user);
        }

        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var login = await LoginEndpointTests.PostLoginAsync(factory.CreateAnonymousClient(), user.Email, UserSeed.Password);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync(CancellationToken.None));
        var token = body.RootElement.GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, token));

        factory.Clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_WithAlteredPayloadKeepingSignature_Returns401()
    {
        using var factory = new ApiFactory();
        var token = TestTokens.ReplacePayload(Token(factory), payload => payload["role"] = "Manager");

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_AlteredToManager_OnTeamMembers_Returns401()
    {
        using var factory = new ApiFactory();
        var employee = Token(factory);
        var altered = TestTokens.ReplacePayload(employee, payload => payload["role"] = "Manager");

        // Control: el token intacto es válido y llega al endpoint (403 por el rol). Alterado, ni siquiera se autentica (AU-22).
        Assert.Equal(HttpStatusCode.Forbidden, await GetTeamMembersAsync(factory, employee));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetTeamMembersAsync(factory, altered));
    }

    private static async Task<HttpStatusCode> GetTeamMembersAsync(ApiFactory factory, string token)
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return (await client.GetAsync("/api/team/members", CancellationToken.None)).StatusCode;
    }

    [Fact]
    public async Task Token_SignedWithHs512_Returns401()
    {
        using var factory = new ApiFactory();
        var token = TestTokens.Create(UserId, "Employee", factory.Clock.GetUtcNow(), algorithm: "HS512");

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_WithAlgNone_Returns401()
    {
        using var factory = new ApiFactory();
        var token = TestTokens.CreateUnsigned(UserId, "Employee", factory.Clock.GetUtcNow());

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_WithOtherIssuer_Returns401()
    {
        using var factory = new ApiFactory();
        var token = TestTokens.Create(UserId, "Employee", factory.Clock.GetUtcNow(), issuer: "otro-emisor");

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_WithOtherAudience_Returns401()
    {
        using var factory = new ApiFactory();
        var token = TestTokens.Create(UserId, "Employee", factory.Clock.GetUtcNow(), audience: "otra-audiencia");

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_SignedWithOtherKey_Returns401()
    {
        using var factory = new ApiFactory();
        var token = TestTokens.Create(UserId, "Employee", factory.Clock.GetUtcNow(), signingKey: "otra-clave-de-firma-distinta-0123456789-xyz");

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_WithRoleAdmin_Returns401()
    {
        using var factory = new ApiFactory();
        var token = TestTokens.Create(UserId, "Admin", factory.Clock.GetUtcNow());

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Token_WithInvalidSub_Returns401(string subject)
    {
        using var factory = new ApiFactory();
        var token = TestTokens.Create(UserId, "Employee", factory.Clock.GetUtcNow(), subject: subject);

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
    }

    [Fact]
    public async Task Token_Expired_WwwAuthenticateHasBearerWithoutErrorDescription()
    {
        using var factory = new ApiFactory();
        var now = factory.Clock.GetUtcNow();
        var token = Token(factory, issuedAt: now - TimeSpan.FromHours(1), expires: now - TimeSpan.FromMinutes(2));

        var response = await MeEndpointTests.GetMeAsync(factory, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = string.Join(' ', response.Headers.WwwAuthenticate.Select(value => value.ToString()));
        Assert.Contains("Bearer", challenge, StringComparison.Ordinal);
        Assert.DoesNotContain("error_description", challenge, StringComparison.Ordinal);
    }

    private static string Token(
        ApiFactory factory,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null)
        => TestTokens.Create(UserId, "Employee", issuedAt ?? factory.Clock.GetUtcNow(), notBefore: notBefore, expires: expires);

    private static async Task<HttpStatusCode> StatusAsync(ApiFactory factory, string token)
        => (await MeEndpointTests.GetMeAsync(factory, token)).StatusCode;
}
