namespace TimeTracker.Api.Registration;

/// <summary>Resultado de un intento de registro, el único dato (con el instante y la compañía creada) que se registra.</summary>
public enum RegistrationAttemptResult
{
    Created,
    CreatedWithoutSession,
    RejectedByInviteCode,
    RejectedByValidation,
    RejectedByDuplicate,
    RejectedByRateLimit,
}

/// <summary>
/// Registro del autorregistro (company-registration, «Privacidad del registro»): solo metadatos. Nunca contraseñas, códigos de
/// invitación (ni su posición en la lista), correos ni nombres; por eso estos métodos no reciben ninguno de ellos.
/// </summary>
public static partial class RegistrationLog
{
    public const string Category = "TimeTracker.Api.Registration";

    public static void Created(ILogger logger, Guid companyId, DateTimeOffset at) => LogCreated(logger, RegistrationAttemptResult.Created, companyId, at);

    /// <summary>La causa es el nombre del tipo de la excepción de la emisión de sesión, nunca su mensaje.</summary>
    public static void CreatedWithoutSession(ILogger logger, Guid companyId, DateTimeOffset at, string cause) => LogCreatedWithoutSession(logger, RegistrationAttemptResult.CreatedWithoutSession, companyId, at, cause);

    public static void Rejected(ILogger logger, RegistrationAttemptResult result, DateTimeOffset at) => LogRejected(logger, result, at);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registro de compañía: resultado {Result}, compañía {CompanyId}, instante {At:O}.")]
    private static partial void LogCreated(ILogger logger, RegistrationAttemptResult result, Guid companyId, DateTimeOffset at);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registro de compañía: resultado {Result}, compañía {CompanyId}, instante {At:O}, causa {Cause}.")]
    private static partial void LogCreatedWithoutSession(ILogger logger, RegistrationAttemptResult result, Guid companyId, DateTimeOffset at, string cause);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registro de compañía: resultado {Result}, instante {At:O}.")]
    private static partial void LogRejected(ILogger logger, RegistrationAttemptResult result, DateTimeOffset at);
}
