using System.Security.Claims;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Account;

public class SessionClaimsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompanyName_IsReadFromTheCnameClaimAsPlainText()
    {
        var user = Tokens.Principal(("cname", "<b>x</b>"), ("cid", "11111111-1111-1111-1111-111111111111"));

        Assert.Equal("<b>x</b>", SessionClaims.CompanyName(user));
    }

    [Fact]
    public void CompanyName_WithoutTheClaim_IsNull()
        => Assert.Null(SessionClaims.CompanyName(Tokens.Principal(("name", "Ana"))));

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    public void PasswordChangePending_OnlyWhenMcpIsExactlyTrue(string value, bool expected)
        => Assert.Equal(expected, SessionClaims.IsPasswordChangePending(Tokens.Principal(("mcp", value))));

    [Fact]
    public void PasswordChangePending_WithoutTheClaimOrAnonymous_IsFalse()
    {
        Assert.False(SessionClaims.IsPasswordChangePending(Tokens.Principal(("name", "Ana"))));
        Assert.False(SessionClaims.IsPasswordChangePending(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void PasswordChangePending_FromAToken_ReadsTheMcpClaim()
    {
        Assert.True(SessionClaims.IsPasswordChangePending(Tokens.WithPendingChange()));
        Assert.False(SessionClaims.IsPasswordChangePending(Tokens.WithoutPendingChange()));
        Assert.False(SessionClaims.IsPasswordChangePending(Tokens.Create(Now, passwordChangePending: false)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-token")]
    [InlineData("a.b.c")]
    public void PasswordChangePending_FromAMalformedToken_IsFalse(string token)
        => Assert.False(SessionClaims.IsPasswordChangePending(token));

    [Fact]
    public async Task TheStateProvider_ExposesCnameAndMcpToTheInterface()
    {
        var js = new FakeSessionStorage();
        js.Seed(TokenStore.StorageKey, Tokens.Create(DateTimeOffset.UtcNow.AddMinutes(30), companyName: "Compañía Ejemplo", passwordChangePending: true));
        var provider = new JwtAuthenticationStateProvider(new TokenStore(js), TimeProvider.System, new SessionNotice());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.Equal("Compañía Ejemplo", SessionClaims.CompanyName(state.User));
        Assert.True(SessionClaims.IsPasswordChangePending(state.User));
    }
}
