using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Api.Assistant;

/// <summary>
/// Deja una línea informativa al arrancar con el estado de la protección de claves (DD-7). Solo el estado y un motivo
/// categórico: nunca versiones ni valores de las claves maestras. Que no esté disponible no impide el arranque.
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
            LogUnavailable(logger, Describe(protector.UnavailableReason));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string Describe(ProtectionUnavailableReason reason) => reason switch
    {
        ProtectionUnavailableReason.NoActiveVersionConfigured => "sin versión activa configurada",
        ProtectionUnavailableReason.ActiveVersionWithoutKey => "la versión activa no tiene clave",
        _ => "clave maestra inválida o duplicada",
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Asistente: protección de claves disponible.")]
    private static partial void LogAvailable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Asistente: protección de claves no disponible (motivo: {Reason}); no se podrán guardar claves del proveedor desde la Web.")]
    private static partial void LogUnavailable(ILogger logger, string reason);
}
