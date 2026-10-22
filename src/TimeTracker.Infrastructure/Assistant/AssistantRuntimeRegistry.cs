using System.Collections.Concurrent;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Registro singleton de instantáneas del asistente por compañía (DD-14). La primera pregunta de una compañía crea su
/// <see cref="CompanyRuntime"/> con una carga única protegida por un <see cref="Lazy{T}"/>; una carga fallida por infraestructura no
/// se cachea. El número de compañías con proveedor cargado está acotado (<c>Ai:MaxLoadedCompanies</c>): al superarlo se expulsa la
/// menos usada recientemente, y las entradas sin preguntas durante <c>Ai:RuntimeIdleMinutes</c> caducan. Una compañía que no es de
/// plataforma nunca recibe los valores ni la clave del entorno.
/// </summary>
internal sealed class AssistantRuntimeRegistry(
    ICompanySettingsReader reader,
    AssistantOptionsResolver resolver,
    IChatClientFactory factory,
    IOptions<AiOptions> options,
    TimeProvider timeProvider,
    ILogger<AssistantRuntimeRegistry> logger) : IAssistantRuntimeRegistry, IDisposable
{
    /// <summary>Plazo de una carga: pasado, se considera un fallo transitorio (no se cachea) y se responde el 503 genérico.</summary>
    internal static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan OverloadWarningInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, Lazy<Task<CompanyRuntime>>> _entries = new();
    private readonly Dictionary<CompanyRuntime, (ITimer Timer, long Sequence)> _expelled = [];
    private readonly Lock _capacityGate = new();
    private long _lastOverloadWarningTicks = long.MinValue;
    private long _expulsions;
    private bool _disposed;

    /// <summary>Entradas vivas del registro, cargadas o en carga (para comprobar que un fallo no deja una entrada fallida).</summary>
    internal int EntryCount => _entries.Count;

    /// <summary>Compañías expulsadas cuyo pipeline sigue en su periodo de gracia.</summary>
    internal int ExpelledCount
    {
        get { lock (_capacityGate) { return _expelled.Count; } }
    }

    public async ValueTask<AssistantSnapshot> GetAsync(Guid companyId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var entry = _entries.GetOrAdd(companyId, NewEntry);
            CompanyRuntime runtime;
            try
            {
                runtime = await entry.Value.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Quien se impacienta no cancela la carga que esperan los demás.
                throw;
            }
            catch
            {
                // Fallo transitorio de infraestructura (base de datos caída o plazo agotado): no se cachea, la siguiente pregunta reintenta.
                _entries.TryRemove(new KeyValuePair<Guid, Lazy<Task<CompanyRuntime>>>(companyId, entry));
                throw;
            }

            // El tope se aplica una sola vez por carga y con la entrada ya publicada, de modo que cuenta con las demás cargas simultáneas.
            if (runtime.TryAdmit() && runtime.IsEnabled)
            {
                EnforceCapacity(runtime);
            }

            if (runtime.TryAcquire(timeProvider.GetUtcNow()) is { } snapshot)
            {
                return snapshot;
            }

            // Se expulsó entre la búsqueda y el uso: se vuelve a cargar desde su fila (la comprobación y la lectura son atómicas).
            _entries.TryRemove(new KeyValuePair<Guid, Lazy<Task<CompanyRuntime>>>(companyId, entry));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public async Task ReloadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (CompletedRuntime(companyId) is not { } runtime)
        {
            return;
        }

        await runtime.ReloadGate.WaitAsync(cancellationToken);
        try
        {
            var read = await reader.ReadAsync(companyId, cancellationToken);
            var version = read.Stored?.Version ?? AssistantSnapshot.EnvironmentVersion;
            var unchangedIndicator = read.UsesPlatform == runtime.UsesPlatform;
            if (runtime.Evicted
                || (unchangedIndicator && (version == runtime.Client.Current.Version || version == runtime.LastRejectedVersion)))
            {
                return;
            }

            Apply(runtime, read, version);
        }
        finally
        {
            runtime.ReloadGate.Release();
        }
    }

    public IReadOnlyList<LoadedRuntime> Loaded() => [.. CompletedRuntimes().Select(runtime => runtime.Describe())];

    public int ExpireIdle()
    {
        var idle = TimeSpan.FromMinutes(options.Value.RuntimeIdleMinutes);
        var now = timeProvider.GetUtcNow();
        var expired = 0;
        lock (_capacityGate)
        {
            foreach (var runtime in CompletedRuntimes().Where(runtime => now - runtime.LastUsed >= idle))
            {
                expired += Evict(runtime) ? 1 : 0;
            }
        }

        return expired;
    }

    public void Dispose()
    {
        List<AssistantChatClient> clients;
        List<ITimer> timers;
        lock (_capacityGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            clients = [.. CompletedRuntimes().Select(runtime => runtime.Client), .. _expelled.Keys.Select(runtime => runtime.Client)];
            timers = [.. _expelled.Values.Select(expelled => expelled.Timer)];
            _expelled.Clear();
            _entries.Clear();
        }

        timers.ForEach(timer => timer.Dispose());
        clients.ForEach(client => client.Dispose());
    }

    // --- Carga inicial ---

    private async Task<CompanyRuntime> FirstLoadAsync(Guid companyId)
    {
        using var timeout = new CancellationTokenSource(LoadTimeout, timeProvider);
        var read = await reader.ReadAsync(companyId, timeout.Token);
        var (client, rejected) = BuildFirst(companyId, read);
        return new CompanyRuntime(companyId, client, read.UsesPlatform, timeProvider.GetUtcNow()) { LastRejectedVersion = rejected };
    }

    // Una carga que falla (o se agota) se retira sola, aunque nadie la espere ya: así un fallo tras abandonar la precarga del arranque
    // no deja una entrada fallida que daría un 503 espurio a la primera pregunta.
    private Lazy<Task<CompanyRuntime>> NewEntry(Guid companyId)
    {
        Lazy<Task<CompanyRuntime>>? entry = null;
        entry = new Lazy<Task<CompanyRuntime>>(() =>
        {
            var load = FirstLoadAsync(companyId);
            _ = load.ContinueWith(
                failed =>
                {
                    _ = failed.Exception;
                    _entries.TryRemove(new KeyValuePair<Guid, Lazy<Task<CompanyRuntime>>>(companyId, entry!));
                },
                CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return load;
        });
        return entry;
    }

    private (AssistantChatClient Client, string? RejectedVersion) BuildFirst(Guid companyId, CompanySettingsRead read)
    {
        var version = read.Stored?.Version ?? AssistantSnapshot.EnvironmentVersion;
        var (resolved, category) = Resolve(companyId, read);
        if (resolved is null)
        {
            // Sin instantánea válida previa: deshabilitada para esa versión; nunca se recurre al entorno ni a su clave.
            Warn(version, category!, null);
            return (new AssistantChatClient(null, resolver.Disabled(), timeProvider, version), version);
        }

        if (!TryCreate(resolved, out var inner, out var exceptionType))
        {
            Warn(version, AssistantOptionsResolver.InvalidCategory, exceptionType);
            return (new AssistantChatClient(null, resolver.Disabled(), timeProvider, version), version);
        }

        Applied(version, resolved);
        return (new AssistantChatClient(inner, resolved, timeProvider, version), null);
    }

    // --- Recarga ---

    private void Apply(CompanyRuntime runtime, CompanySettingsRead read, string version)
    {
        var companyId = runtime.CompanyId;
        var (resolved, category) = Resolve(companyId, read);
        string? exceptionType = null;
        IChatClient? inner = null;
        if (resolved is not null && !TryCreate(resolved, out inner, out exceptionType))
        {
            resolved = null;
            category = AssistantOptionsResolver.InvalidCategory;
        }

        if (resolved is not null)
        {
            if (runtime.TrySwap(inner, resolved, version, read.UsesPlatform))
            {
                runtime.LastRejectedVersion = null;
                Applied(version, resolved);
                EnforceCapacity(runtime);
            }

            return;
        }

        if (runtime.LastRejectedVersion != version)
        {
            runtime.LastRejectedVersion = version;
            Warn(version, category!, exceptionType);
        }

        // Se conserva la última configuración válida, salvo que la compañía haya perdido el indicador de plataforma: esa configuración
        // pudo construirse con la clave del entorno y no debe seguir sirviéndose (aviso de la revisión de la PR 4a).
        if (runtime.BuiltWithPlatform && !read.UsesPlatform)
        {
            runtime.TrySwap(null, resolver.Disabled(), version, usesPlatform: false);
        }
        else
        {
            // Si se ganó el indicador con la fila rechazada, se anota para que el sondeo no la relea en cada vuelta; la configuración
            // vigente se construyó sin los valores de plataforma, así que perderlo después no la degrada.
            runtime.ObserveIndicator(read.UsesPlatform);
        }
    }

    // --- Resolución y creación del cliente ---

    private (AiOptions? Options, string? Category) Resolve(Guid companyId, CompanySettingsRead read)
        => read.Stored is { } stored
            ? resolver.Resolve(stored, read.UsesPlatform, companyId)
            : (read.UsesPlatform ? resolver.Platform() : resolver.Disabled(), null);

    private bool TryCreate(AiOptions resolved, out IChatClient? inner, out string? exceptionType)
    {
        if (!resolved.IsEnabled)
        {
            // Sin proveedor no hay pipeline ni cliente HTTP que construir.
            inner = null;
            exceptionType = null;
            return true;
        }

        try
        {
            inner = factory.Create(resolved);
            exceptionType = null;
            return true;
        }
        catch (Exception exception)
        {
            inner = null;
            exceptionType = exception.GetType().Name;
            return false;
        }
    }

    // --- Tope y expulsión ---

    private void EnforceCapacity(CompanyRuntime keep)
    {
        var limit = options.Value.MaxLoadedCompanies;
        lock (_capacityGate)
        {
            if (_disposed || keep.Evicted)
            {
                return;
            }

            var others = CompletedRuntimes().Where(runtime => runtime != keep && runtime.IsEnabled && !runtime.Evicted).OrderBy(runtime => runtime.LastUsed).ToList();
            var excess = others.Count + 1 - limit;
            var evicted = 0;
            foreach (var victim in others.Take(Math.Max(0, excess)))
            {
                evicted += Evict(victim) ? 1 : 0;
            }

            if (evicted > 0)
            {
                WarnOverloaded(limit);
            }
        }
    }

    private bool Evict(CompanyRuntime runtime)
    {
        if (!runtime.Evict(resolver.Disabled()))
        {
            return false;
        }

        if (_entries.TryGetValue(runtime.CompanyId, out var entry) && entry.Value.IsCompletedSuccessfully && ReferenceEquals(entry.Value.Result, runtime))
        {
            _entries.TryRemove(new KeyValuePair<Guid, Lazy<Task<CompanyRuntime>>>(runtime.CompanyId, entry));
        }

        // El pipeline anterior lo libera su gracia de 120 s; el soporte se descarta a la vez, salvo que el registro se cierre antes.
        _expelled[runtime] = (timeProvider.CreateTimer(ReleaseExpelled, runtime, AssistantChatClient.ReleaseGracePeriod, Timeout.InfiniteTimeSpan), ++_expulsions);
        BoundExpelled();
        return true;
    }

    // La cola de expulsadas no crece con la tasa de recarga: como mucho Ai:MaxLoadedCompanies pipelines esperan su gracia, de modo que la
    // memoria retenida queda acotada por el doble del tope. Si se desborda, se libera ya el más antiguo (el más cercano a su gracia).
    private void BoundExpelled()
    {
        var bound = Math.Max(1, options.Value.MaxLoadedCompanies);
        while (_expelled.Count > bound)
        {
            var oldest = _expelled.MinBy(expelled => expelled.Value.Sequence);
            _expelled.Remove(oldest.Key);
            oldest.Value.Timer.Dispose();
            oldest.Key.Client.Dispose();
        }
    }

    private void ReleaseExpelled(object? state)
    {
        var runtime = (CompanyRuntime)state!;
        (ITimer Timer, long Sequence) expelled;
        lock (_capacityGate)
        {
            if (!_expelled.Remove(runtime, out expelled))
            {
                return;
            }
        }

        expelled.Timer.Dispose();
        runtime.Client.Dispose();
    }

    private void WarnOverloaded(int limit)
    {
        var now = timeProvider.GetUtcNow().UtcTicks;
        if (_lastOverloadWarningTicks != long.MinValue && now - _lastOverloadWarningTicks < OverloadWarningInterval.Ticks)
        {
            return;
        }

        _lastOverloadWarningTicks = now;
        AssistantSettingsLog.Overloaded(logger, limit);
    }

    // --- Acceso a las entradas ---

    private CompanyRuntime? CompletedRuntime(Guid companyId)
        => _entries.TryGetValue(companyId, out var entry) && entry.IsValueCreated && entry.Value.IsCompletedSuccessfully ? entry.Value.Result : null;

    private List<CompanyRuntime> CompletedRuntimes()
        => [.. _entries.Values.Where(entry => entry.IsValueCreated && entry.Value.IsCompletedSuccessfully).Select(entry => entry.Value.Result)];

    private void Applied(string version, AiOptions resolved) => AssistantSettingsLog.Applied(logger, version, resolved.Provider ?? "None");

    private void Warn(string version, string category, string? exceptionType)
    {
        if (exceptionType is null)
        {
            AssistantSettingsLog.Rejected(logger, version, category);
        }
        else
        {
            AssistantSettingsLog.RejectedWithException(logger, version, category, exceptionType);
        }
    }
}
