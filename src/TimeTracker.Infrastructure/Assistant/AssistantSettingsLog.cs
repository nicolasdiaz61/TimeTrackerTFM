using Microsoft.Extensions.Logging;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Registros de la recarga de la configuración (DD-11). Solo versión, categoría y nombre de tipo de la excepción:
/// nunca la clave, su forma cifrada ni el mensaje de una excepción, que puede contenerlos.
/// </summary>
internal static partial class AssistantSettingsLog
{
    [LoggerMessage(EventId = 10, Level = LogLevel.Information,
        Message = "Configuración del asistente aplicada (versión {Version}, proveedor {Provider}).")]
    public static partial void Applied(ILogger logger, string version, string provider);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning,
        Message = "Configuración guardada del asistente rechazada (versión {Version}, motivo {Category}); se conserva la última válida.")]
    public static partial void Rejected(ILogger logger, string version, string category);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning,
        Message = "Configuración guardada del asistente rechazada (versión {Version}, motivo {Category}, {ExceptionType}); se conserva la última válida.")]
    public static partial void RejectedWithException(ILogger logger, string version, string category, string exceptionType);

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning,
        Message = "No se pudo recargar la configuración del asistente tras guardarla ({ExceptionType}); el sondeo lo reintentará.")]
    public static partial void ReloadFailed(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 14, Level = LogLevel.Warning,
        Message = "No se pudo consultar la configuración guardada del asistente ({ExceptionType}); se conserva la última válida.")]
    public static partial void PollFailed(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information,
        Message = "Se recuperó la consulta de la configuración guardada del asistente.")]
    public static partial void PollRecovered(ILogger logger);

    [LoggerMessage(EventId = 16, Level = LogLevel.Warning,
        Message = "La primera carga de la configuración del asistente superó {Seconds} s; la compañía de plataforma cargará su configuración al recibir la primera pregunta.")]
    public static partial void FirstLoadTimedOut(ILogger logger, int seconds);

    [LoggerMessage(EventId = 17, Level = LogLevel.Warning,
        Message = "Un suscriptor de PollCompleted lanzó una excepción ({ExceptionType}); el sondeo continúa.")]
    public static partial void PollCompletedSubscriberFailed(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 18, Level = LogLevel.Warning,
        Message = "Hay más compañías con asistente activo que el tope Ai:MaxLoadedCompanies ({Limit}): se expulsan las menos usadas y se recargan al preguntar; suba el tope si se repite.")]
    public static partial void Overloaded(ILogger logger, int limit);
}
