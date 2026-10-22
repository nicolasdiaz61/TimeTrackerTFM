using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Team;

public abstract record CreateTeamMemberResult
{
    private CreateTeamMemberResult()
    {
    }

    /// <summary>Persona creada; <paramref name="TemporaryPassword"/> es la única vez que existe en claro.</summary>
    public sealed record Created(UserAccount User, string TemporaryPassword) : CreateTeamMemberResult;

    /// <summary>Campos inválidos; los mensajes nombran el campo y nunca repiten la contraseña.</summary>
    public sealed record Invalid(IReadOnlyList<FieldError> Errors) : CreateTeamMemberResult;

    /// <summary>El correo ya existe en alguna compañía; no se indica en cuál ni con qué rol.</summary>
    public sealed record DuplicateEmail : CreateTeamMemberResult;

    /// <summary>La compañía ya tiene tantos usuarios como permite el tope.</summary>
    public sealed record CapReached : CreateTeamMemberResult;

    /// <summary>Demasiadas altas simultáneas: no se creó nada y conviene repetir la operación.</summary>
    public sealed record Busy : CreateTeamMemberResult;
}
