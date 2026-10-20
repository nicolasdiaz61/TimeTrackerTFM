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

    /// <summary>Efecto de una carga correcta (por ejemplo, publicar la versión en el cliente).</summary>
    public Action? OnLoad { get; set; }

    private readonly TaskCompletionSource _loadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Se completa cuando empieza la primera carga: señal explícita en lugar de sondear el contador.</summary>
    public Task LoadStarted => _loadStarted.Task;

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
        _loadStarted.TrySetResult();
        if (LoadGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        if (LoadFailure is { } failure)
        {
            throw failure;
        }

        OnLoad?.Invoke();
        return SettingsLoadOutcome.Applied;
    }
}
