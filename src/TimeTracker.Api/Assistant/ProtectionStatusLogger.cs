using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Api.Assistant;

/// <summary>
/// Deja una línea informativa al arrancar con el estado de la protección de claves (DD-7). Solo el estado: nunca
/// versiones ni valores de las claves maestras. Que no esté disponible no impide el arranque.
/// </summary>
public sealed partial class ProtectionStatusLogger(IAssistantCredentialProtector protector, ILogger<ProtectionStatusLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (protector.IsAvailable)
        {
            LogAvailable(logger);
        }
        else
        {
            LogUnavailable(logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Asistente: protección de claves disponible.")]
    private static partial void LogAvailable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Asistente: protección de claves no disponible; no se podrán guardar claves del proveedor desde la Web.")]
    private static partial void LogUnavailable(ILogger logger);
}
