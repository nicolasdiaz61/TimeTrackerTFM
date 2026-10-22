using Microsoft.Extensions.Logging;

using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantRuntimeReloaderTests
{
    private const string Secret = "clave-prueba-123456";

    private static readonly Guid CompanyA = Guid.Parse("00000000-0000-4000-8000-0000000000a1");

    [Fact]
    public async Task ReloadAsync_ReloadsTheGivenCompanyOnceAndLogsNothingOnSuccess()
    {
        var registry = new FakeRuntimeRegistry();
        var logger = new CapturingLogger<AssistantRuntimeReloader>();

        await new AssistantRuntimeReloader(registry, logger).ReloadAsync(CompanyA, CancellationToken.None);

        Assert.Equal([CompanyA], registry.Reloads);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task ReloadAsync_WhenTheRegistryFails_LogsAWarningWithOnlyTheExceptionTypeAndRethrows()
    {
        var registry = new FakeRuntimeRegistry { OnReload = (_, _) => throw new InvalidOperationException($"fallo con {Secret}") };
        var logger = new CapturingLogger<AssistantRuntimeReloader>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new AssistantRuntimeReloader(registry, logger).ReloadAsync(CompanyA, CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(nameof(InvalidOperationException), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fallo con", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReloadAsync_WhenCancelled_RethrowsWithoutLoggingAFailure()
    {
        var gate = new TaskCompletionSource();
        var registry = new FakeRuntimeRegistry { OnReload = (_, token) => gate.Task.WaitAsync(token) };
        var logger = new CapturingLogger<AssistantRuntimeReloader>();
        using var cancellation = new CancellationTokenSource();

        var pending = new AssistantRuntimeReloader(registry, logger).ReloadAsync(CompanyA, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Empty(logger.Entries);
    }
}
