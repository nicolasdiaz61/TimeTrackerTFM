using Microsoft.Extensions.Logging;

using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantRuntimeReloaderTests
{
    private const string Secret = "clave-prueba-123456";

    [Fact]
    public async Task ReloadAsync_RunsTheLoaderOnceAndLogsNothingOnSuccess()
    {
        var loader = new FakeSettingsLoader();
        var logger = new CapturingLogger<AssistantRuntimeReloader>();

        await new AssistantRuntimeReloader(loader, logger).ReloadAsync(CancellationToken.None);

        Assert.Equal(1, loader.Loads);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task ReloadAsync_WhenTheLoaderFails_LogsAWarningWithOnlyTheExceptionTypeAndRethrows()
    {
        var loader = new FakeSettingsLoader { LoadFailure = new InvalidOperationException($"fallo con {Secret}") };
        var logger = new CapturingLogger<AssistantRuntimeReloader>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new AssistantRuntimeReloader(loader, logger).ReloadAsync(CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(nameof(InvalidOperationException), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fallo con", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReloadAsync_WhenCancelled_RethrowsWithoutLoggingAFailure()
    {
        var loader = new FakeSettingsLoader { LoadGate = new TaskCompletionSource() };
        var logger = new CapturingLogger<AssistantRuntimeReloader>();
        using var cancellation = new CancellationTokenSource();

        var pending = new AssistantRuntimeReloader(loader, logger).ReloadAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Empty(logger.Entries);
    }
}
