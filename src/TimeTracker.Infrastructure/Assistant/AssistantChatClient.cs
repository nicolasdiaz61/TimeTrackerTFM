using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Soporte de la <see cref="AssistantSnapshot"/> vigente de UNA compañía (DD-10, DD-14): sin cliente si no tiene proveedor configurado.
/// Lo posee el registro de instantáneas, que lo libera al cerrar o al expulsar la compañía, y con él los clientes del proveedor, vigente y pendientes.
/// </summary>
public sealed class AssistantChatClient : IDisposable
{
    // Toda petición está acotada por TimeoutSeconds (<= 55 s): 120 s cubre cualquier petición iniciada antes del cambio sin contadores de uso.
    internal static readonly TimeSpan ReleaseGracePeriod = TimeSpan.FromSeconds(120);

    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly List<(AssistantSnapshot Snapshot, ITimer Timer)> _retired = [];
    private AssistantSnapshot _current;
    private bool _disposed;

    /// <param name="inner">Cliente del proveedor, o <c>null</c> si el asistente está deshabilitado.</param>
    public AssistantChatClient(IChatClient? inner, AiOptions options, TimeProvider? timeProvider = null, string? version = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _current = AssistantSnapshot.Create(inner, options, version ?? AssistantSnapshot.EnvironmentVersion);
    }

    public bool IsEnabled => Current.Pipeline is not null;

    /// <summary>Instantánea vigente. Quien la use durante una petición debe leerla una sola vez.</summary>
    internal AssistantSnapshot Current => Volatile.Read(ref _current);

    /// <summary>Publica la instantánea nueva y devuelve la anterior, que se libera tras el periodo de gracia.</summary>
    internal AssistantSnapshot Swap(IChatClient? inner, AiOptions options, string version)
    {
        var next = AssistantSnapshot.Create(inner, options, version);
        lock (_gate)
        {
            if (_disposed)
            {
                next.Pipeline?.Dispose();
                throw new ObjectDisposedException(nameof(AssistantChatClient));
            }

            var previous = Interlocked.Exchange(ref _current, next);
            _retired.Add((previous, _timeProvider.CreateTimer(Release, previous, ReleaseGracePeriod, Timeout.InfiniteTimeSpan)));
            return previous;
        }
    }

    public void Dispose()
    {
        List<(AssistantSnapshot Snapshot, ITimer Timer)> pending;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            pending = [.. _retired];
            _retired.Clear();
        }

        foreach (var (snapshot, timer) in pending)
        {
            timer.Dispose();
            snapshot.Pipeline?.Dispose();
        }

        _current.Pipeline?.Dispose();
    }

    private void Release(object? state)
    {
        var snapshot = (AssistantSnapshot)state!;
        ITimer timer;
        lock (_gate)
        {
            var index = _retired.FindIndex(entry => ReferenceEquals(entry.Snapshot, snapshot));
            if (index < 0)
            {
                return;
            }

            timer = _retired[index].Timer;
            _retired.RemoveAt(index);
        }

        timer.Dispose();
        snapshot.Pipeline?.Dispose();
    }
}
