using Microsoft.Extensions.Logging;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Registro de auditoría del asistente (DD-12): solo metadatos (qué, quién, resultado y duración). Nunca argumentos,
/// fechas, identificadores de terceros, nombres ni contenido devuelto.
/// </summary>
internal static partial class AssistantLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Herramienta del asistente {Tool} por {EmployeeId}: {Outcome} en {ElapsedMs} ms")]
    public static partial void ToolExecuted(ILogger logger, string tool, Guid employeeId, string outcome, long elapsedMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Consulta del asistente por {EmployeeId} ({Provider}/{Model}): {Outcome} en {ElapsedMs} ms, tokens {InputTokens}/{OutputTokens}, herramientas {ToolCalls}")]
    public static partial void QueryCompleted(
        ILogger logger, Guid employeeId, string? provider, string? model, string outcome, long elapsedMs, long? inputTokens, long? outputTokens, int toolCalls);

    // Solo el nombre del tipo de la excepción: su mensaje puede contener la clave, el texto del usuario o datos de la herramienta.
    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Consulta del asistente por {EmployeeId} ({Provider}/{Model}): {Outcome} en {ElapsedMs} ms ({ExceptionType})")]
    public static partial void QueryFailed(
        ILogger logger, Guid employeeId, string? provider, string? model, string outcome, long elapsedMs, string exceptionType);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "No se pudo cargar la configuración del asistente de la compañía ({ExceptionType}); la petición recibe «no disponible» y la siguiente reintenta.")]
    public static partial void RuntimeLoadFailed(ILogger logger, string exceptionType);
}
