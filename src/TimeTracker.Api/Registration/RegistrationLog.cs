namespace TimeTracker.Api.Registration;

/// <summary>Resultado de un intento de registro, el único dato (con el instante y la compañía creada) que se registra.</summary>
public enum RegistrationAttemptResult
{
    Created,
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

    public static void Rejected(ILogger logger, RegistrationAttemptResult result, DateTimeOffset at) => LogRejected(logger, result, at);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registro de compañía: resultado {Result}, compañía {CompanyId}, instante {At:O}.")]
    private static partial void LogCreated(ILogger logger, RegistrationAttemptResult result, Guid companyId, DateTimeOffset at);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registro de compañía: resultado {Result}, instante {At:O}.")]
    private static partial void LogRejected(ILogger logger, RegistrationAttemptResult result, DateTimeOffset at);
}
