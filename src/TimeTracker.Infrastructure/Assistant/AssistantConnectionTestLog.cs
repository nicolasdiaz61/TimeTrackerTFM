using Microsoft.Extensions.Logging;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Registros de la prueba de conexión (DD-13). Solo proveedor, categoría y nombre de tipo de la excepción: nunca el modelo,
/// la clave ni el mensaje de la excepción, que puede traer la URL o la clave.
/// </summary>
internal static partial class AssistantConnectionTestLog
{
    [LoggerMessage(EventId = 20, Level = LogLevel.Information,
        Message = "Prueba de conexión del asistente ({Provider}): {Category}.")]
    public static partial void Succeeded(ILogger logger, string provider, string category);

    [LoggerMessage(EventId = 21, Level = LogLevel.Warning,
        Message = "Prueba de conexión del asistente ({Provider}): {Category} ({ExceptionType}).")]
    public static partial void Failed(ILogger logger, string provider, string category, string exceptionType);
}
