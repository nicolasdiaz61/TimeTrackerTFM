using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Sondeo global de la configuración guardada (DD-15). Cada vuelta lee en una sola consulta la versión (<c>xmin</c>) y el indicador de
/// plataforma de todas las compañías y recarga, con un máximo de recargas en paralelo, solo las compañías ya cargadas cuya versión o
/// indicador cambió; las no cargadas se ignoran (cargan fresca al preguntar). También caduca las entradas inactivas. Una caída de la
/// base de datos conserva las instantáneas vigentes, avisa una vez por racha (solo el nombre del tipo de la excepción) y deja una
/// línea al recuperarse. La primera carga acotada de <c>StartAsync</c> precarga solo la compañía de plataforma: las migraciones corren en
/// <c>StartingAsync</c> y Kestrel arranca después de todos los <c>StartAsync</c> registrados antes.
/// </summary>
internal sealed class AssistantSettingsPoller(
    IAssistantRuntimeRegistry registry,
    IServiceScopeFactory scopes,
    IOptions<AiOptions> options,
    TimeProvider timeProvider,
    ILogger<AssistantSettingsPoller> logger) : BackgroundService
{
    internal const int FirstLoadTimeoutSeconds = 10;
    internal const int MaxParallelReloads = 4;

    private PeriodicTimer? _timer;
    private bool _failing;

    /// <summary>
    /// Se levanta al terminar cada vuelta de sondeo (también la primera carga y las vueltas fallidas), cuando las instantáneas ya están
    /// publicadas. Es la señal que usan las pruebas para esperar sin depender del tiempo real.
    /// </summary>
    // Se invoca suscriptor a suscriptor y sin propagar: uno que lance no debe romper el bucle de sondeo (con StopHost detendría el host).
    public event Action? PollCompleted;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await PreloadPlatformCompanyAsync(cancellationToken);
        // El temporizador se crea aquí y no en ExecuteAsync: el host puede empezar a ejecutarlo más tarde y el primer intervalo
        // debe contarse desde el arranque.
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SettingsPollSeconds), timeProvider);
        await base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(stoppingToken))
            {
                await PollAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // Acotada para no retrasar el arranque con una base de datos lenta: hasta que acabe, la compañía de plataforma carga al preguntar.
    private async Task PreloadPlatformCompanyAsync(CancellationToken startToken)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(FirstLoadTimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(startToken, limit.Token);
        try
        {
            await registry.GetAsync(Company.DefaultId, linked.Token);
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !startToken.IsCancellationRequested)
        {
            AssistantSettingsLog.FirstLoadTimedOut(logger, FirstLoadTimeoutSeconds);
        }
        catch (Exception exception) when (!startToken.IsCancellationRequested)
        {
            // Un fallo transitorio no impide arrancar: la carga se reintenta en la primera pregunta.
            MarkFailing(exception);
        }
        finally
        {
            RaisePollCompleted();
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            registry.ExpireIdle();
            var failures = await ReloadChangedAsync(cancellationToken);
            if (failures.IsEmpty)
            {
                MarkRecovered();
            }
            else
            {
                failures.TryPeek(out var first);
                MarkFailing(first!);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            MarkFailing(exception);
        }
        finally
        {
            RaisePollCompleted();
        }
    }

    private async Task<ConcurrentQueue<Exception>> ReloadChangedAsync(CancellationToken cancellationToken)
    {
        var failures = new ConcurrentQueue<Exception>();
        var loaded = registry.Loaded();
        if (loaded.Count == 0)
        {
            return failures;
        }

        IReadOnlyList<CompanyVersion> versions;
        await using (var scope = scopes.CreateAsyncScope())
        {
            versions = await scope.ServiceProvider.GetRequiredService<IAssistantSettingsStore>().ListVersionsAsync(cancellationToken);
        }

        var byCompany = versions.ToDictionary(version => version.CompanyId);
        var changed = loaded.Where(runtime => Changed(runtime, byCompany)).ToList();
        await Parallel.ForEachAsync(
            changed,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelReloads, CancellationToken = cancellationToken },
            async (runtime, token) =>
            {
                try
                {
                    await registry.ReloadAsync(runtime.CompanyId, token);
                }
                catch (Exception exception) when (!token.IsCancellationRequested)
                {
                    failures.Enqueue(exception);
                }
            });
        return failures;
    }

    // Una fila nueva para una entrada «0» cuenta como cambio; una versión ya rechazada no se vuelve a leer. Una compañía sin fila
    // tiene la versión «0».
    private static bool Changed(LoadedRuntime runtime, Dictionary<Guid, CompanyVersion> byCompany)
    {
        var row = byCompany.GetValueOrDefault(runtime.CompanyId);
        var version = row?.Version ?? AssistantSnapshot.EnvironmentVersion;
        var usesPlatform = row?.UsesPlatformAssistant ?? runtime.UsesPlatform;
        return usesPlatform != runtime.UsesPlatform
            || (version != runtime.Version && version != runtime.LastRejectedVersion);
    }

    private void MarkFailing(Exception exception)
    {
        if (!_failing)
        {
            _failing = true;
            AssistantSettingsLog.PollFailed(logger, exception.GetType().Name);
        }
    }

    private void MarkRecovered()
    {
        if (_failing)
        {
            _failing = false;
            AssistantSettingsLog.PollRecovered(logger);
        }
    }

    private void RaisePollCompleted()
    {
        foreach (var subscriber in PollCompleted?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action)subscriber)();
            }
            catch (Exception exception)
            {
                AssistantSettingsLog.PollCompletedSubscriberFailed(logger, exception.GetType().Name);
            }
        }
    }
}
