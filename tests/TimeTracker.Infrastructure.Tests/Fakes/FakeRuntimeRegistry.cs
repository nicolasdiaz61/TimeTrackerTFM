using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>
/// Registro de instantáneas guionizado: sirve la instantánea vigente de un <see cref="AssistantChatClient"/> a cualquier compañía y
/// registra las preguntas, las recargas y las caducidades. Permite probar el motor, el recargador y el sondeo sin base de datos.
/// </summary>
internal sealed class FakeRuntimeRegistry(AssistantChatClient? client = null) : IAssistantRuntimeRegistry
{
    public List<Guid> Gets { get; } = [];

    public List<Guid> Reloads { get; } = [];

    public List<LoadedRuntime> LoadedRuntimes { get; } = [];

    public int Expirations { get; private set; }

    public Exception? GetFailure { get; set; }

    public Func<Guid, CancellationToken, Task>? OnReload { get; set; }

    private int _inFlightReloads;
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Número de recargas simultáneas a partir del cual se completa <see cref="Reached"/>.</summary>
    public int SignalAtInFlight { get; set; } = int.MaxValue;

    public Task Reached => _reached.Task;

    public int MaxConcurrentReloads { get; private set; }

    public ValueTask<AssistantSnapshot> GetAsync(Guid companyId, CancellationToken cancellationToken)
    {
        Gets.Add(companyId);
        if (GetFailure is { } failure)
        {
            throw failure;
        }

        return ValueTask.FromResult(client!.Current);
    }

    public async Task ReloadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        lock (Reloads)
        {
            Reloads.Add(companyId);
        }

        var inFlight = Interlocked.Increment(ref _inFlightReloads);
        MaxConcurrentReloads = Math.Max(MaxConcurrentReloads, inFlight);
        if (inFlight >= SignalAtInFlight)
        {
            _reached.TrySetResult();
        }

        try
        {
            if (OnReload is { } onReload)
            {
                await onReload(companyId, cancellationToken);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlightReloads);
        }
    }

    public IReadOnlyList<LoadedRuntime> Loaded() => [.. LoadedRuntimes];

    public int ExpireIdle()
    {
        Expirations++;
        return 0;
    }
}
