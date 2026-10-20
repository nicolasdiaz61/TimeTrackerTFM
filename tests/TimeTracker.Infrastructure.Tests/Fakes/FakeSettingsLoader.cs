using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>Cargador guionizado para probar al recargador y al sondeo sin base de datos.</summary>
internal sealed class FakeSettingsLoader : IAssistantSettingsLoader
{
    public string StoredVersion { get; set; } = "0";

    public string? LastRejectedVersion { get; set; }

    public Exception? ReadFailure { get; set; }

    public Exception? LoadFailure { get; set; }

    /// <summary>Si se asigna, la carga espera a que se complete (o se cancele) antes de terminar.</summary>
    public TaskCompletionSource? LoadGate { get; set; }

    public int Loads { get; private set; }

    public int Reads { get; private set; }

    public Task<string> ReadVersionAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return ReadFailure is { } failure ? throw failure : Task.FromResult(StoredVersion);
    }

    public async Task<SettingsLoadOutcome> LoadAsync(CancellationToken cancellationToken)
    {
        Loads++;
        if (LoadGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        return LoadFailure is { } failure ? throw failure : SettingsLoadOutcome.Applied;
    }
}
