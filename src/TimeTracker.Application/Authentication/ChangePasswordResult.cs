using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Application.Authentication;

public abstract record ChangePasswordResult
{
    private ChangePasswordResult()
    {
    }

    /// <summary>Contraseña cambiada; <paramref name="Session"/> es la sesión nueva, sin cambio de contraseña pendiente.</summary>
    public sealed record Changed(LoginResult Session) : ChangePasswordResult;

    /// <summary>Campos inválidos; los mensajes nombran el campo y nunca repiten ninguna contraseña.</summary>
    public sealed record Invalid(IReadOnlyList<FieldError> Errors) : ChangePasswordResult;

    /// <summary>La persona o su compañía ya no pueden iniciar sesión: el mismo 401 genérico del inicio de sesión.</summary>
    public sealed record Unauthorized : ChangePasswordResult;

    /// <summary>La fila de la persona cambió entre la lectura y el guardado (por ejemplo, un Manager regeneró la temporal).</summary>
    public sealed record Conflict : ChangePasswordResult;
}
