using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Auth;

public class SessionSynchronizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeSessionStorage storage = new();
    private readonly SessionNotice notice = new();
    private readonly FakeNavigation navigation = new();
    private readonly TokenStore store;
    private readonly JwtAuthenticationStateProvider provider;
    private readonly SessionSynchronizer synchronizer;

    public SessionSynchronizerTests()
    {
        store = new TokenStore(storage);
        provider = new JwtAuthenticationStateProvider(store, new FakeTimeProvider(Now), notice);
        synchronizer = new SessionSynchronizer(store, provider, navigation);
    }

    [Fact]
    public async Task LogoutInAnotherTab_SignsOutAndGoesToLogin()
    {
        await store.SetAsync(Tokens.Create(Now.AddMinutes(30)));
        navigation.GoTo("fichajes");
        await synchronizer.StartAsync();

        await storage.RaiseAsync(TokenStore.StorageKey, null);

        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);
        Assert.Equal("login", navigation.LastNavigation);
        Assert.False(notice.Consume());
    }

    [Fact]
    public async Task LogoutInAnotherTab_WhileAlreadyOnLogin_DoesNotNavigate()
    {
        await store.SetAsync(Tokens.Create(Now.AddMinutes(30)));
        navigation.GoTo("login?returnUrl=%2Ffichajes");
        await synchronizer.StartAsync();

        await storage.RaiseAsync(TokenStore.StorageKey, null);

        Assert.Null(navigation.LastNavigation);
    }

    [Fact]
    public async Task LogoutInAnotherTab_OnAnAnonymousPageWithoutSession_DoesNotNavigate()
    {
        navigation.GoTo("registro");
        await synchronizer.StartAsync();

        await storage.RaiseAsync(TokenStore.StorageKey, null);

        Assert.Null(navigation.LastNavigation);
    }

    [Fact]
    public async Task LoginInAnotherTab_LetsTheTabOnLoginContinue()
    {
        navigation.GoTo("login");
        await synchronizer.StartAsync();

        await storage.RaiseAsync(TokenStore.StorageKey, Tokens.Create(Now.AddMinutes(30)));

        Assert.True((await provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);
        Assert.Equal("/", navigation.LastNavigation);
    }

    [Fact]
    public async Task LoginInAnotherTab_DoesNotMoveATabThatIsNotOnLogin()
    {
        navigation.GoTo("registro");
        await synchronizer.StartAsync();

        await storage.RaiseAsync(TokenStore.StorageKey, Tokens.Create(Now.AddMinutes(30)));

        Assert.Null(navigation.LastNavigation);
    }

    [Fact]
    public async Task TokenReplacedInAnotherTab_IsPickedUpWithoutNavigating()
    {
        await store.SetAsync(Tokens.Create(Now.AddMinutes(5)));
        navigation.GoTo("cambiar-contrasena");
        await synchronizer.StartAsync();
        var replacement = Tokens.Create(Now.AddMinutes(30));

        await storage.RaiseAsync(TokenStore.StorageKey, replacement);

        Assert.Equal(replacement, await store.GetAsync());
        Assert.Null(navigation.LastNavigation);
    }

    [Fact]
    public async Task AnExpiredTokenFromAnotherTab_IsTreatedAsExpired()
    {
        await store.SetAsync(Tokens.Create(Now.AddMinutes(30)));
        navigation.GoTo("fichajes");
        await synchronizer.StartAsync();

        await storage.RaiseAsync(TokenStore.StorageKey, Tokens.Create(Now.AddMinutes(-1)));

        Assert.Equal("login", navigation.LastNavigation);
        Assert.True(notice.Consume());
    }

    [Fact]
    public async Task Start_IsIdempotent_AndDisposeStopsListening()
    {
        await synchronizer.StartAsync();
        await synchronizer.StartAsync();
        Assert.True(storage.Listening);

        await synchronizer.DisposeAsync();

        Assert.False(storage.Listening);
    }

    private sealed class FakeNavigation : NavigationManager
    {
        public FakeNavigation() => Initialize("http://localhost/", "http://localhost/");

        public string? LastNavigation { get; private set; }

        public void GoTo(string relative) => Uri = "http://localhost/" + relative;

        protected override void NavigateToCore(string uri, NavigationOptions options) => LastNavigation = uri;
    }
}
