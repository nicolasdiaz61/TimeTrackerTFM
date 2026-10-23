using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Time.Testing;

using TimeTracker.Web.Auth;

namespace TimeTracker.Web.Tests;

public class JwtAuthenticationStateProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeSessionStorage js = new();
    private readonly SessionNotice notice = new();
    private readonly JwtAuthenticationStateProvider provider;

    public JwtAuthenticationStateProviderTests()
        => provider = new JwtAuthenticationStateProvider(new TokenStore(js), new FakeTimeProvider(Now), notice);

    [Fact]
    public async Task ExpiredStoredToken_IsDiscardedAndMarksTheSessionAsExpired()
    {
        js.Seed(TokenStore.StorageKey, TokenExpiringAt(Now.AddMinutes(-1)));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.True(notice.Consume());
    }

    [Fact]
    public async Task ValidStoredToken_AuthenticatesWithoutExpiryNotice()
    {
        js.Seed(TokenStore.StorageKey, TokenExpiringAt(Now.AddMinutes(30)));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.False(notice.Consume());
    }

    [Fact]
    public async Task NoStoredToken_IsAnonymousWithoutExpiryNotice()
    {
        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.False(notice.Consume());
    }

    [Fact]
    public async Task VoluntarySignOut_DiscardsAPendingExpiryNotice()
    {
        notice.MarkExpired();

        await provider.SignOutAsync();

        Assert.False(notice.Consume());
    }

    [Fact]
    public async Task SignIn_DiscardsAPendingExpiryNotice()
    {
        notice.MarkExpired();

        await provider.SignInAsync(TokenExpiringAt(Now.AddMinutes(30)));

        Assert.False(notice.Consume());
    }

    private static string TokenExpiringAt(DateTimeOffset expiry)
    {
        var payload = JsonSerializer.Serialize(new { sub = "u1", name = "Usuario Ejemplo", exp = expiry.ToUnixTimeSeconds() });
        return $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url(payload)}.firma";
    }

    private static string Base64Url(string text)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
