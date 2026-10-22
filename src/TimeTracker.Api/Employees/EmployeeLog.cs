namespace TimeTracker.Api.Employees;

/// <summary>Resultado de una operación sobre personas, el único dato (con el instante y los identificadores) que se registra.</summary>
public enum EmployeeOperationResult
{
    Created,
    Regenerated,
    RejectedByValidation,
    RejectedByDuplicate,
    RejectedByCap,
    RejectedByContention,
    NotFound,
    Conflict,
}

/// <summary>
/// Registro del alta y la regeneración (employee-management): solo metadatos (instante, resultado e identificadores del Manager y de la
/// persona). Nunca contraseñas, correos ni nombres; por eso estos métodos no reciben ninguno de ellos.
/// </summary>
public static partial class EmployeeLog
{
    public const string Category = "TimeTracker.Api.Employees";

    public static void Operation(ILogger logger, EmployeeOperationResult result, Guid managerId, Guid? memberId, DateTimeOffset at)
        => LogOperation(logger, result, managerId, memberId, at);

    [LoggerMessage(Level = LogLevel.Information, Message = "Operación sobre personas: resultado {Result}, responsable {ManagerId}, persona {MemberId}, instante {At:O}.")]
    private static partial void LogOperation(ILogger logger, EmployeeOperationResult result, Guid managerId, Guid? memberId, DateTimeOffset at);
}
