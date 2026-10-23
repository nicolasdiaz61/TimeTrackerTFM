using TimeTracker.Web.Auth;

namespace TimeTracker.Web.Tests.Auth;

public class TokenStoreTests
{
    private readonly FakeSessionStorage storage = new();

    [Fact]
    public async Task Get_RestoresTheTokenStoredByAnotherTab()
    {
        storage.Seed(TokenStore.StorageKey, "abc");

        Assert.Equal("abc", await new TokenStore(storage).GetAsync());
    }

    [Fact]
    public async Task Get_MigratesAnOldPerTabSessionToTheSharedStorageOnce()
    {
        storage.SeedLegacy(TokenStore.StorageKey, "viejo");

        var token = await new TokenStore(storage).GetAsync();

        Assert.Equal("viejo", token);
        Assert.Equal("viejo", storage.Stored(TokenStore.StorageKey));
        Assert.Null(storage.StoredLegacy(TokenStore.StorageKey));
    }

    [Fact]
    public async Task Get_PrefersTheSharedTokenAndStillRemovesTheOldOne()
    {
        storage.Seed(TokenStore.StorageKey, "nuevo");
        storage.SeedLegacy(TokenStore.StorageKey, "viejo");

        Assert.Equal("nuevo", await new TokenStore(storage).GetAsync());
        Assert.Null(storage.StoredLegacy(TokenStore.StorageKey));
    }

    [Fact]
    public async Task Set_WritesToTheSharedStorage()
    {
        await new TokenStore(storage).SetAsync("abc");

        Assert.Equal("abc", storage.Stored(TokenStore.StorageKey));
    }

    [Fact]
    public async Task Clear_RemovesTheStoredSessionAndTheMemoryCopy()
    {
        var store = new TokenStore(storage);
        await store.SetAsync("abc");

        await store.ClearAsync();

        Assert.Null(storage.Stored(TokenStore.StorageKey));
        Assert.Null(await store.GetAsync());
    }

    [Fact]
    public async Task Get_KeepsServingTheMemoryCopyWhenTheKeyIsRemovedLocally()
    {
        var store = new TokenStore(storage);
        await store.SetAsync("abc");
        await store.StartListeningAsync((_, _) => Task.CompletedTask);
        await storage.RaiseAsync("otra.clave", null);

        Assert.Equal("abc", await store.GetAsync());
    }

    [Fact]
    public async Task BlockedStorage_DegradesToAnInMemorySession()
    {
        storage.Blocked = true;
        var store = new TokenStore(storage);

        Assert.Null(await store.GetAsync());
        await store.SetAsync("abc");
        Assert.Equal("abc", await store.GetAsync());
        await store.StartListeningAsync((_, _) => Task.CompletedTask);
        await store.ClearAsync();
        Assert.Null(await store.GetAsync());
    }

    [Fact]
    public async Task ExternalChange_UpdatesMemoryAndReportsPreviousAndCurrent()
    {
        var store = new TokenStore(storage);
        await store.SetAsync("uno");
        var calls = new List<(string? Previous, string? Current)>();
        await store.StartListeningAsync((previous, current) =>
        {
            calls.Add((previous, current));
            return Task.CompletedTask;
        });

        await storage.RaiseAsync(TokenStore.StorageKey, "dos");
        await storage.RaiseAsync(TokenStore.StorageKey, null);

        Assert.Equal([("uno", "dos"), ("dos", (string?)null)], calls);
        Assert.Null(await store.GetAsync());
    }

    [Fact]
    public async Task ExternalChange_OfAnotherKeyIsIgnored_AndAClearAllRemovesTheSession()
    {
        var store = new TokenStore(storage);
        await store.SetAsync("uno");
        var calls = 0;
        await store.StartListeningAsync((_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await storage.RaiseAsync("otra.clave", "x");
        Assert.Equal(0, calls);

        await storage.RaiseAsync(null, null);
        Assert.Equal(1, calls);
        Assert.Null(await store.GetAsync());
    }

    [Fact]
    public async Task OwnWrites_NeverReachTheListener()
    {
        var store = new TokenStore(storage);
        var calls = 0;
        await store.StartListeningAsync((_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await store.SetAsync("abc");
        await store.ClearAsync();

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Dispose_StopsListening()
    {
        var store = new TokenStore(storage);
        await store.StartListeningAsync((_, _) => Task.CompletedTask);
        Assert.True(storage.Listening);

        await store.DisposeAsync();

        Assert.False(storage.Listening);
    }
}
