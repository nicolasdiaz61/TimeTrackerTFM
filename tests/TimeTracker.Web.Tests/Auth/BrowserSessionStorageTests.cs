using TimeTracker.Web.Auth;

namespace TimeTracker.Web.Tests.Auth;

public class BrowserSessionStorageTests
{
    private readonly FakeJsRuntime js = new();

    [Fact]
    public async Task GetSetRemove_GoThroughTheJsModule()
    {
        await using var storage = new BrowserSessionStorage(js);

        await storage.SetAsync("k", "v");
        Assert.Equal("v", await storage.GetAsync("k"));
        await storage.RemoveAsync("k");
        Assert.Null(await storage.GetAsync("k"));
    }

    [Fact]
    public async Task TakeLegacy_ReadsAndRemovesTheOldPerTabValue()
    {
        js.SeedLegacy("k", "viejo");
        await using var storage = new BrowserSessionStorage(js);

        Assert.Equal("viejo", await storage.TakeLegacyAsync("k"));
        Assert.Null(await storage.TakeLegacyAsync("k"));
    }

    [Fact]
    public async Task StorageEvent_ReachesTheRegisteredCallback_AndStopsAfterUnsubscribing()
    {
        await using var storage = new BrowserSessionStorage(js);
        var received = new List<(string?, string?)>();
        await storage.ListenAsync((key, value) =>
        {
            received.Add((key, value));
            return Task.CompletedTask;
        });

        await storage.OnStorageChanged("k", "v");
        await storage.StopListeningAsync();
        await storage.OnStorageChanged("k", null);

        Assert.Equal([("k", (string?)"v")], received);
        Assert.NotNull(js.SubscribeArguments);
        Assert.Contains("timeTrackerSession.unsubscribe", js.Calls);
    }

    [Fact]
    public async Task StopListening_WithoutListening_DoesNothing()
    {
        await using var storage = new BrowserSessionStorage(js);

        await storage.StopListeningAsync();

        Assert.DoesNotContain("timeTrackerSession.unsubscribe", js.Calls);
    }
}
