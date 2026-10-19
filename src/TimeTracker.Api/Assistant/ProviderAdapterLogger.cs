using Microsoft.Extensions.Options;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Api.Assistant;

/// <summary>
/// Avisa al arrancar de que el proveedor configurado aún no tiene adaptador, así que el asistente queda deshabilitado
/// sin que el arranque falle. Solo el nombre del proveedor: nunca el modelo ni la clave.
/// </summary>
public sealed partial class ProviderAdapterLogger(IOptions<AiOptions> options, ILogger<ProviderAdapterLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (ChatClientFactory.LacksAdapter(options.Value) && options.Value.ResolvedProvider is { } provider)
        {
            LogWithoutAdapter(logger, provider);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Asistente: el proveedor {Provider} aún no tiene adaptador; el asistente queda deshabilitado.")]
    private static partial void LogWithoutAdapter(ILogger logger, AiProvider provider);
}
