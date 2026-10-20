using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>Almacén en memoria de la fila única; permite simular una base de datos caída y observar la concurrencia de lecturas.</summary>
internal sealed class FakeAssistantSettingsStore : IAssistantSettingsStore
{
    private int _inFlight;

    public AssistantSettings? Stored { get; set; }

    public Exception? Failure { get; set; }

    /// <summary>Si se asigna, <see cref="GetAsync"/> espera a que se complete antes de responder.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public int GetCalls { get; private set; }

    public int VersionCalls { get; private set; }

    public int MaxConcurrentReads { get; private set; }

    public async Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken)
    {
        GetCalls++;
        MaxConcurrentReads = Math.Max(MaxConcurrentReads, Interlocked.Increment(ref _inFlight));
        try
        {
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            return Failure is { } failure ? throw failure : Stored;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    public Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        VersionCalls++;
        return Failure is { } failure ? throw failure : Task.FromResult(Stored?.Version ?? "0");
    }

    public Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
