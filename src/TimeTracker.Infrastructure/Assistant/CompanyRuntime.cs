using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Estado publicado de una compañía cargada, tal como lo ve el sondeo.</summary>
internal sealed record LoadedRuntime(Guid CompanyId, string Version, bool UsesPlatform, string? LastRejectedVersion);

/// <summary>
/// Soporte de la instantánea de una compañía (DD-14): el cliente con su gracia de liberación, el último uso (para expulsar la menos
/// usada) y las marcas de la última versión rechazada y del indicador de plataforma con el que se construyó.
/// </summary>
internal sealed class CompanyRuntime(Guid companyId, AssistantChatClient client, bool usesPlatform, DateTimeOffset now)
{
    private readonly Lock _gate = new();
    private long _lastUsedTicks = now.UtcTicks;
    private bool _usesPlatform = usesPlatform;
    private string? _lastRejectedVersion;
    private bool _evicted;

    public Guid CompanyId { get; } = companyId;

    public AssistantChatClient Client { get; } = client;

    /// <summary>Serializa las recargas de esta compañía: lee siempre la fila fresca y no publica una versión más antigua que la aplicada.</summary>
    public SemaphoreSlim ReloadGate { get; } = new(1, 1);

    public bool UsesPlatform
    {
        get { lock (_gate) { return _usesPlatform; } }
    }

    public string? LastRejectedVersion
    {
        get { lock (_gate) { return _lastRejectedVersion; } }
        set { lock (_gate) { _lastRejectedVersion = value; } }
    }

    public bool Evicted
    {
        get { lock (_gate) { return _evicted; } }
    }

    public bool IsEnabled => Client.IsEnabled;

    public DateTimeOffset LastUsed => new(Interlocked.Read(ref _lastUsedTicks), TimeSpan.Zero);

    public void Touch(DateTimeOffset now) => Interlocked.Exchange(ref _lastUsedTicks, now.UtcTicks);

    /// <summary>Publica la instantánea nueva salvo que la compañía ya haya sido expulsada (así una recarga tardía no resucita un pipeline huérfano).</summary>
    public bool TrySwap(IChatClient? inner, AiOptions options, string version, bool usesPlatform)
    {
        lock (_gate)
        {
            if (_evicted)
            {
                inner?.Dispose();
                return false;
            }

            Client.Swap(inner, options, version);
            _usesPlatform = usesPlatform;
            return true;
        }
    }

    /// <summary>Marca la compañía como expulsada y publica la instantánea deshabilitada: el pipeline anterior se libera tras la gracia.</summary>
    public bool Evict(AiOptions disabled)
    {
        lock (_gate)
        {
            if (_evicted)
            {
                return false;
            }

            _evicted = true;
            Client.Swap(null, disabled, AssistantSnapshot.EnvironmentVersion);
            return true;
        }
    }

    public LoadedRuntime Describe()
    {
        lock (_gate)
        {
            return new LoadedRuntime(CompanyId, Client.Current.Version, _usesPlatform, _lastRejectedVersion);
        }
    }
}
