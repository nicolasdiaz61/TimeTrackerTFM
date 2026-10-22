using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Estado publicado de una compañía cargada, tal como lo ve el sondeo.</summary>
internal sealed record LoadedRuntime(Guid CompanyId, string Version, bool UsesPlatform, string? LastRejectedVersion);

/// <summary>
/// Soporte de la instantánea de una compañía (DD-14): el cliente con su gracia de liberación, el último uso (para expulsar la menos
/// usada) y las marcas de la última versión rechazada y del indicador de plataforma. Distingue el indicador <em>observado</em> en la
/// última lectura (<see cref="UsesPlatform"/>, lo que compara el sondeo) del indicador con el que se <em>construyó</em> la
/// configuración vigente (<see cref="BuiltWithPlatform"/>, lo que decide si debe degradarse al perderlo).
/// </summary>
internal sealed class CompanyRuntime(Guid companyId, AssistantChatClient client, bool usesPlatform, DateTimeOffset now)
{
    private readonly Lock _gate = new();
    private long _lastUsedTicks = now.UtcTicks;
    private bool _usesPlatform = usesPlatform;
    private bool _builtWithPlatform = usesPlatform;
    private string? _lastRejectedVersion;
    private bool _evicted;
    private int _admitted;

    public Guid CompanyId { get; } = companyId;

    public AssistantChatClient Client { get; } = client;

    /// <summary>Serializa las recargas de esta compañía: lee siempre la fila fresca y no publica una versión más antigua que la aplicada.</summary>
    public SemaphoreSlim ReloadGate { get; } = new(1, 1);

    /// <summary>Indicador de plataforma visto en la última lectura de la fila (aplicada o rechazada).</summary>
    public bool UsesPlatform
    {
        get { lock (_gate) { return _usesPlatform; } }
    }

    /// <summary>Si la configuración vigente pudo construirse con los valores o la clave del entorno.</summary>
    public bool BuiltWithPlatform
    {
        get { lock (_gate) { return _builtWithPlatform; } }
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

    /// <summary>
    /// Registra el uso y devuelve la instantánea vigente, o <c>null</c> si la compañía ya fue expulsada (quien llama debe recargarla). La
    /// comprobación y la lectura comparten sección crítica con <see cref="Evict"/>: nunca se devuelve la instantánea deshabilitada que deja la expulsión.
    /// </summary>
    public AssistantSnapshot? TryAcquire(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_evicted)
            {
                return null;
            }

            Touch(now);
            return Client.Current;
        }
    }

    /// <summary>Concede una sola vez el derecho a aplicar el tope tras publicarse la carga inicial (así cuenta con las demás ya publicadas).</summary>
    public bool TryAdmit() => Interlocked.Exchange(ref _admitted, 1) == 0;

    /// <summary>Anota el indicador visto cuando la fila se rechazó: la configuración vigente no cambia, pero la lectura no debe repetirse.</summary>
    public void ObserveIndicator(bool usesPlatform)
    {
        lock (_gate)
        {
            _usesPlatform = usesPlatform;
        }
    }

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
            _builtWithPlatform = usesPlatform;
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
