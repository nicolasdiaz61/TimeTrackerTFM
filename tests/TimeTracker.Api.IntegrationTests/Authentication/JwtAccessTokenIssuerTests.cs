using System.Buffers.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Api.Authentication;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Authentication;

public class JwtAccessTokenIssuerTests
{
    private const string StoredHash = "hash-almacenado-que-no-debe-salir";

    private const string CompanyName = "Acme Norte";

    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);

    [Fact]
    public void Issue_ContainsAllClaimsAndHs256Header()
    {
        var user = NewUser();

        var token = CreateIssuer().Issue(user, CompanyName).Value;

        var (header, payload) = Decode(token);
        Assert.Equal("HS256", header.GetProperty("alg").GetString());
        Assert.Equal(user.Id.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal("Marta Ejemplo", payload.GetProperty("name").GetString());
        Assert.Equal("marta.responsable@example.test", payload.GetProperty("email").GetString());
        Assert.Equal("Manager", payload.GetProperty("role").GetString());
        Assert.True(Guid.TryParse(payload.GetProperty("jti").GetString(), out _));
        Assert.Equal("timetracker-api", payload.GetProperty("iss").GetString());
        Assert.Equal("timetracker-web", payload.GetProperty("aud").GetString());
        foreach (var name in new[] { "iat", "nbf", "exp" })
        {
            Assert.Equal(JsonValueKind.Number, payload.GetProperty(name).ValueKind);
        }
    }

    [Fact]
    public void Issue_ExpiresThirtyMinutesAfterNow()
    {
        var accessToken = CreateIssuer().Issue(NewUser(), CompanyName);

        var (_, payload) = Decode(accessToken.Value);
        var expected = new DateTimeOffset(2026, 10, 12, 8, 30, 0, TimeSpan.Zero);
        Assert.Equal(expected.ToUnixTimeSeconds(), payload.GetProperty("exp").GetInt64());
        Assert.Equal(Now.ToUnixTimeSeconds(), payload.GetProperty("iat").GetInt64());
        Assert.Equal(Now.ToUnixTimeSeconds(), payload.GetProperty("nbf").GetInt64());
        Assert.Equal(expected, accessToken.ExpiresAt);
    }

    [Fact]
    public void Issue_WithFractionalSeconds_ExpiresAtMatchesTheExpClaim()
    {
        _clock.Advance(TimeSpan.FromMilliseconds(700));

        var accessToken = CreateIssuer().Issue(NewUser(), CompanyName);

        // La respuesta del login y el claim exp deben coincidir: el token solo guarda segundos enteros.
        var (_, payload) = Decode(accessToken.Value);
        Assert.Equal(accessToken.ExpiresAt.ToUnixTimeSeconds(), payload.GetProperty("exp").GetInt64());
        Assert.Equal(0, accessToken.ExpiresAt.Millisecond);
    }

    [Fact]
    public void Issue_DoesNotContainHashOrPassword()
    {
        var token = CreateIssuer().Issue(NewUser(), CompanyName).Value;

        var (_, payload) = Decode(token);
        var text = payload.GetRawText();
        Assert.DoesNotContain(StoredHash, text, StringComparison.Ordinal);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Issue_TwoTokensHaveDifferentJti()
    {
        var issuer = CreateIssuer();
        var user = NewUser();

        var first = Decode(issuer.Issue(user, CompanyName).Value).Payload.GetProperty("jti").GetString();
        var second = Decode(issuer.Issue(user, CompanyName).Value).Payload.GetProperty("jti").GetString();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Issue_CarriesTheCompanyIdAndNameClaimsAndNoPasswordChangeMarker()
    {
        var companyId = Guid.Parse("f3c5a0de-5b6e-4e77-8a3b-2d8c9b1e4a77");

        var (_, payload) = Decode(CreateIssuer().Issue(NewUser(companyId: companyId), CompanyName).Value);

        Assert.Equal(companyId.ToString(), payload.GetProperty("cid").GetString());
        Assert.Equal(CompanyName, payload.GetProperty("cname").GetString());
        Assert.False(payload.TryGetProperty("mcp", out _));
    }

    [Fact]
    public void Issue_WithAPendingPasswordChange_AddsTheMcpClaimAsTrue()
    {
        var (_, payload) = Decode(CreateIssuer().Issue(NewUser(mustChangePassword: true), CompanyName).Value);

        Assert.Equal("true", payload.GetProperty("mcp").GetString());
    }

    [Fact]
    public void Issue_NeverWritesACompanyIdPropertyName()
    {
        var (_, payload) = Decode(CreateIssuer().Issue(NewUser(), CompanyName).Value);

        Assert.False(payload.TryGetProperty("companyId", out _));
    }

    private static UserAccount NewUser(bool mustChangePassword = false, Guid? companyId = null)
        => UserAccount.Create(Guid.NewGuid(), companyId ?? Company.DefaultId, "Marta.Responsable@example.test", "Marta Ejemplo", StoredHash, UserRole.Manager, managerId: null, mustChangePassword);

    private JwtAccessTokenIssuer CreateIssuer()
        => new(Options.Create(new JwtOptions { SigningKey = ApiFactory.ValidSigningKey }), _clock);

    // Se decodifica a mano (base64url + JSON): la prueba no depende de la biblioteca que emite el token.
    private static (JsonElement Header, JsonElement Payload) Decode(string token)
    {
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
        using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        return (header.RootElement.Clone(), payload.RootElement.Clone());
    }
}
