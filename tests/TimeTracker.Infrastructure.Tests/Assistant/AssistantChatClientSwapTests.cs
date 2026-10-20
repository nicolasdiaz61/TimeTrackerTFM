using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantChatClientSwapTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(120);

    private static AiOptions Options(string model, int timeout = 25) => new() { Provider = "Ollama", Model = model, Endpoint = "http://localhost:11434/", TimeoutSeconds = timeout };

    [Fact]
    public void TheCompatibleConstructor_PublishesTheEnvironmentSnapshotWithVersionZero()
    {
        var inner = new DisposalTrackingChatClient();
        using var client = new AssistantChatClient(inner, Options("modelo-a"));

        Assert.True(client.IsEnabled);
        Assert.Equal("0", client.Current.Version);
        Assert.Equal("modelo-a", client.Current.Options.Model);
        Assert.NotNull(client.Current.Pipeline);
    }

    [Fact]
    public void TheCompatibleConstructor_WithoutAnInnerClient_IsDisabled()
    {
        using var client = new AssistantChatClient(null, new AiOptions());

        Assert.False(client.IsEnabled);
        Assert.Null(client.Current.Pipeline);
    }

    [Fact]
    public void Swap_PublishesTheNewSnapshotAndReturnsThePreviousOne()
    {
        using var client = new AssistantChatClient(new DisposalTrackingChatClient(), Options("modelo-a"), new FakeTimeProvider());
        var first = client.Current;

        var previous = client.Swap(new DisposalTrackingChatClient(), Options("modelo-b", timeout: 40), "17");

        Assert.Same(first, previous);
        Assert.NotSame(first, client.Current);
        Assert.Equal(("modelo-b", 40, "17"), (client.Current.Options.Model, client.Current.Options.TimeoutSeconds, client.Current.Version));
    }

    [Fact]
    public void Swap_ToNoClient_DisablesTheAssistantAndBackAgain()
    {
        using var client = new AssistantChatClient(new DisposalTrackingChatClient(), Options("modelo-a"), new FakeTimeProvider());

        client.Swap(null, new AiOptions(), "5");
        Assert.False(client.IsEnabled);

        client.Swap(new DisposalTrackingChatClient(), Options("modelo-c"), "6");
        Assert.True(client.IsEnabled);
    }

    [Fact]
    public void Swap_KeepsADefensiveCopyOfTheOptionsWithoutTheSecrets()
    {
        using var client = new AssistantChatClient(null, new AiOptions(), new FakeTimeProvider());
        var options = new AiOptions { Provider = "Anthropic", Model = "modelo-a", ApiKey = "clave-prueba-123456", MasterKeys = { ["1"] = "clave-maestra" } };

        client.Swap(new DisposalTrackingChatClient(), options, "9");
        options.Model = "modelo-modificado";

        Assert.Equal("modelo-a", client.Current.Options.Model);
        Assert.Null(client.Current.Options.ApiKey);
        Assert.Empty(client.Current.Options.MasterKeys);
    }

    [Fact]
    public async Task ThePreviousSnapshot_KeepsWorkingUntilItIsReleased()
    {
        var time = new FakeTimeProvider();
        using var client = new AssistantChatClient(new DisposalTrackingChatClient("anterior"), Options("modelo-a"), time);
        var previous = client.Swap(new DisposalTrackingChatClient("nuevo"), Options("modelo-b"), "1");

        time.Advance(Grace - TimeSpan.FromSeconds(1));
        var response = await previous.Pipeline!.GetResponseAsync("hola");

        Assert.Equal("anterior", response.Text);
    }

    [Fact]
    public void ThePreviousSnapshot_IsReleasedAfter120SecondsAndNotBefore()
    {
        var time = new FakeTimeProvider();
        var first = new DisposalTrackingChatClient();
        var second = new DisposalTrackingChatClient();
        using var client = new AssistantChatClient(first, Options("modelo-a"), time);

        client.Swap(second, Options("modelo-b"), "1");
        time.Advance(Grace - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, first.DisposeCount);

        time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, second.DisposeCount);
    }

    [Fact]
    public void SeveralSwapsInARow_ReleaseEachRetiredSnapshotOnceAtItsOwnDeadline()
    {
        var time = new FakeTimeProvider();
        var a = new DisposalTrackingChatClient();
        var b = new DisposalTrackingChatClient();
        var c = new DisposalTrackingChatClient();
        var d = new DisposalTrackingChatClient();
        using var client = new AssistantChatClient(a, Options("a"), time);

        client.Swap(b, Options("b"), "1");
        time.Advance(TimeSpan.FromSeconds(30));
        client.Swap(c, Options("c"), "2");
        client.Swap(d, Options("d"), "3");

        time.Advance(TimeSpan.FromSeconds(90));
        Assert.Equal([1, 0, 0, 0], [a.DisposeCount, b.DisposeCount, c.DisposeCount, d.DisposeCount]);

        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal([1, 1, 1, 0], [a.DisposeCount, b.DisposeCount, c.DisposeCount, d.DisposeCount]);

        time.Advance(TimeSpan.FromHours(1));
        Assert.Equal([1, 1, 1, 0], [a.DisposeCount, b.DisposeCount, c.DisposeCount, d.DisposeCount]);
    }

    [Fact]
    public void Dispose_ReleasesTheCurrentAndThePendingSnapshotsExactlyOnce()
    {
        var time = new FakeTimeProvider();
        var a = new DisposalTrackingChatClient();
        var b = new DisposalTrackingChatClient();
        var c = new DisposalTrackingChatClient();
        var client = new AssistantChatClient(a, Options("a"), time);
        client.Swap(b, Options("b"), "1");
        client.Swap(c, Options("c"), "2");

        client.Dispose();
        client.Dispose();
        time.Advance(TimeSpan.FromHours(1));

        Assert.Equal([1, 1, 1], [a.DisposeCount, b.DisposeCount, c.DisposeCount]);
    }

    [Fact]
    public void Swap_AfterDispose_ThrowsAndReleasesTheNewClient()
    {
        var client = new AssistantChatClient(null, new AiOptions(), new FakeTimeProvider());
        client.Dispose();
        var late = new DisposalTrackingChatClient();

        Assert.Throws<ObjectDisposedException>(() => client.Swap(late, Options("tarde"), "1"));
        Assert.Equal(1, late.DisposeCount);
    }
}
