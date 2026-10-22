using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Authentication;

namespace TimeTracker.Application.Companies;

public abstract record RegisterCompanyResult
{
    private RegisterCompanyResult()
    {
    }

    /// <summary>Compañía y Manager creados; <paramref name="Session"/> es la misma sesión que daría el inicio de sesión y <paramref name="CompanyId"/> la compañía nueva (solo para el registro de metadatos).</summary>
    public sealed record Created(LoginResult Session, Guid CompanyId) : RegisterCompanyResult;

    /// <summary>Compañía y Manager creados, pero no se pudo emitir la sesión; quien llama NO debe repetir el registro.</summary>
    public sealed record CreatedWithoutSession(Guid CompanyId) : RegisterCompanyResult;

    /// <summary>Código ausente, en blanco o incorrecto: una sola respuesta que no dice cuál fue la causa.</summary>
    public sealed record InvalidInviteCode : RegisterCompanyResult;

    /// <summary>Campos inválidos; los mensajes nombran el campo y nunca repiten la contraseña.</summary>
    public sealed record Invalid(IReadOnlyList<FieldError> Errors) : RegisterCompanyResult;

    public sealed record DuplicateName : RegisterCompanyResult;

    public sealed record DuplicateEmail : RegisterCompanyResult;
}
