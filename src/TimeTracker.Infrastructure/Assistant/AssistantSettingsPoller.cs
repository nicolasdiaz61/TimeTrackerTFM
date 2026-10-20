using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Sondea la versión (<c>xmin</c>) de la configuración guardada y carga la fila solo si cambió (DD-11). Lo registra Api, no
/// <c>AddAssistant()</c>. Una caída de la base de datos conserva la última configuración válida, avisa una vez por racha
/// (solo el nombre del tipo de la excepción) y deja una línea al recuperarse. La primera carga se hace en <c>StartAsync</c>:
/// las migraciones corren en <c>StartingAsync</c> y Kestrel arranca después de todos los <c>StartAsync</c> registrados antes.
/// </summary>
public sealed class AssistantSettingsPoller(
    IAssistantSettingsLoader loader,
    AssistantChatClient client,
    IOptions<AiOptions> options,
    TimeProvider timeProvider,
    ILogger<AssistantSettingsPoller> logger) : BackgroundService
{
    internal const int FirstLoadTimeoutSeconds = 10;

    private PeriodicTimer? _timer;
    private bool _failing;

    /// <summary>
    /// Se levanta al terminar cada vuelta de sondeo (también la primera carga y las vueltas fallidas), cuando la instantánea
    /// ya está publicada. Es la señal que usan las pruebas para esperar sin depender del tiempo real.
    /// </summary>
    // Público y no internal porque lo consume Api.IntegrationTests, otro ensamblado. Se invoca suscriptor a suscriptor y sin
    // propagar: uno que lance no debe romper el bucle de sondeo (con StopHost detendría el host) ni a los demás suscriptores.
    public event Action? PollCompleted;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadFirstTimeAsync(cancellationToken);
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

    // Acotada para no retrasar el arranque con una base de datos lenta: hasta que acabe, el asistente usa el entorno.
    private async Task LoadFirstTimeAsync(CancellationToken startToken)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(FirstLoadTimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(startToken, limit.Token);
        try
        {
            await PollAsync(linked.Token);
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !startToken.IsCancellationRequested)
        {
            AssistantSettingsLog.FirstLoadTimedOut(logger, FirstLoadTimeoutSeconds);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            var version = await loader.ReadVersionAsync(cancellationToken);
            if (version != client.Current.Version && version != loader.LastRejectedVersion)
            {
                await loader.LoadAsync(cancellationToken);
            }

            if (_failing)
            {
                _failing = false;
                AssistantSettingsLog.PollRecovered(logger);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (!_failing)
            {
                _failing = true;
                AssistantSettingsLog.PollFailed(logger, exception.GetType().Name);
            }
        }
        finally
        {
            RaisePollCompleted();
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
