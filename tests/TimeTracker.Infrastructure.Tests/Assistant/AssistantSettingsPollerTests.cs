using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantSettingsPollerTests : IAsyncLifetime
{
    private const string Secret = "clave-prueba-123456";
    private const int PollSeconds = 7;

    private readonly FakeTimeProvider _time = new();
    private readonly FakeSettingsLoader _loader = new();
    private readonly CapturingLogger<AssistantSettingsPoller> _logger = new();
    private readonly AssistantChatClient _client;
    private readonly AssistantSettingsPoller _poller;

    public AssistantSettingsPollerTests()
    {
        _client = new AssistantChatClient(null, new AiOptions(), _time);
        _loader.OnLoad = () => _client.Swap(null, new AiOptions(), _loader.StoredVersion);
        _poller = new AssistantSettingsPoller(_loader, _client, Options.Create(new AiOptions { SettingsPollSeconds = PollSeconds }), _time, _logger);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _poller.StopAsync(CancellationToken.None);
        _poller.Dispose();
        _client.Dispose();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "La condición esperada no se cumplió a tiempo.");
            await Task.Delay(5);
        }
    }

    private async Task TickAsync(int expectedReads)
    {
        _time.Advance(TimeSpan.FromSeconds(PollSeconds));
        await WaitUntilAsync(() => _loader.Reads >= expectedReads);
        await Task.Delay(20);
    }

    [Fact]
    public async Task Poll_LoadsOnlyWhenTheVersionChanges()
    {
        await _poller.StartAsync(CancellationToken.None);
        Assert.Equal((1, 0), (_loader.Reads, _loader.Loads));

        await TickAsync(2);
        Assert.Equal(0, _loader.Loads);

        _loader.StoredVersion = "5";
        await TickAsync(3);
        Assert.Equal(1, _loader.Loads);

        await TickAsync(4);
        Assert.Equal(1, _loader.Loads);

        _loader.StoredVersion = "6";
        await TickAsync(5);
        Assert.Equal(2, _loader.Loads);
    }

    [Fact]
    public async Task Poll_DoesNotReloadAVersionThatWasRejected()
    {
        await _poller.StartAsync(CancellationToken.None);
        _loader.StoredVersion = "21";
        _loader.LastRejectedVersion = "21";

        await TickAsync(2);
        await TickAsync(3);
        Assert.Equal(0, _loader.Loads);

        _loader.StoredVersion = "22";
        await TickAsync(4);
        Assert.Equal(1, _loader.Loads);
    }

    [Fact]
    public async Task Poll_BeforeTheIntervalElapses_DoesNotReadAnything()
    {
        await _poller.StartAsync(CancellationToken.None);

        _time.Advance(TimeSpan.FromSeconds(PollSeconds - 1));
        await Task.Delay(50);

        Assert.Equal(1, _loader.Reads);
    }

    [Fact]
    public async Task Poll_WhenTheDatabaseIsDown_KeepsTheLastConfigurationAndWarnsOncePerStreakWithOnlyTheTypeName()
    {
        await _poller.StartAsync(CancellationToken.None);
        _loader.ReadFailure = new TimeoutException($"sin conexión con {Secret}");

        await TickAsync(2);
        await TickAsync(3);
        await TickAsync(4);

        var warning = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(nameof(TimeoutException), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, warning.Message, StringComparison.Ordinal);
        Assert.Equal(0, _loader.Loads);
    }

    [Fact]
    public async Task Poll_WhenTheDatabaseRecovers_LogsOneInformationLineAndAWarningAgainOnTheNextStreak()
    {
        await _poller.StartAsync(CancellationToken.None);
        _loader.ReadFailure = new TimeoutException();
        await TickAsync(2);

        _loader.ReadFailure = null;
        await TickAsync(3);
        await TickAsync(4);

        Assert.Equal([LogLevel.Warning, LogLevel.Information], _logger.Entries.Select(entry => entry.Level));

        _loader.ReadFailure = new TimeoutException();
        await TickAsync(5);

        Assert.Equal([LogLevel.Warning, LogLevel.Information, LogLevel.Warning], _logger.Entries.Select(entry => entry.Level));
    }

    [Fact]
    public async Task Poll_WhenTheLoadFails_CountsAsTheSameStreak()
    {
        await _poller.StartAsync(CancellationToken.None);
        _loader.StoredVersion = "8";
        _loader.LoadFailure = new InvalidOperationException($"fallo con {Secret}");

        await TickAsync(2);
        await TickAsync(3);

        var warning = Assert.Single(_logger.Entries);
        Assert.Contains(nameof(InvalidOperationException), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_RunsTheFirstLoadBeforeReturning()
    {
        _loader.StoredVersion = "9";

        await _poller.StartAsync(CancellationToken.None);

        Assert.Equal(1, _loader.Loads);
        Assert.Equal("9", _client.Current.Version);
    }

    [Fact]
    public async Task StartAsync_WhenTheFirstLoadFails_DoesNotThrowAndWarnsOnce()
    {
        _loader.StoredVersion = "9";
        _loader.LoadFailure = new InvalidOperationException("fallo");

        await _poller.StartAsync(CancellationToken.None);

        Assert.Equal(LogLevel.Warning, Assert.Single(_logger.Entries).Level);
        Assert.Equal("0", _client.Current.Version);
    }

    [Fact]
    public async Task StartAsync_BoundsTheFirstLoadTo10SecondsAndKeepsPolling()
    {
        _loader.StoredVersion = "9";
        _loader.LoadGate = new TaskCompletionSource();

        var starting = _poller.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _loader.Loads == 1);
        _time.Advance(TimeSpan.FromSeconds(9));
        await Task.Delay(30);
        Assert.False(starting.IsCompleted);

        _time.Advance(TimeSpan.FromSeconds(1));
        await starting.WaitAsync(TimeSpan.FromSeconds(5));

        var warning = Assert.Single(_logger.Entries);
        Assert.Contains("10", warning.Message, StringComparison.Ordinal);
        _loader.LoadGate = null;
        await TickAsync(2);
        Assert.Equal("9", _client.Current.Version);
    }

    [Fact]
    public async Task StartAsync_WhenTheHostCancelsTheStart_Propagates()
    {
        _loader.StoredVersion = "9";
        _loader.LoadGate = new TaskCompletionSource();
        using var cancellation = new CancellationTokenSource();

        var starting = _poller.StartAsync(cancellation.Token);
        await WaitUntilAsync(() => _loader.Loads == 1);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
    }

    [Fact]
    public async Task StopAsync_StopsPolling()
    {
        await _poller.StartAsync(CancellationToken.None);
        await _poller.StopAsync(CancellationToken.None);

        _time.Advance(TimeSpan.FromSeconds(PollSeconds * 3));
        await Task.Delay(50);

        Assert.Equal(1, _loader.Reads);
    }
}
