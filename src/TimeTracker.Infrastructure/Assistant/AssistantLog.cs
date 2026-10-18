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
}
