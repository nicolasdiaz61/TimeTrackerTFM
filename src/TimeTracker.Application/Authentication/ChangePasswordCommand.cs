namespace TimeTracker.Application.Authentication;

/// <summary>Las dos contraseñas del cambio. No lleva identificadores: la persona es siempre la del token.</summary>
public sealed record ChangePasswordCommand(string? CurrentPassword, string? NewPassword)
{
    // Nunca se imprimen las contraseñas (el registro de un record usa ToString).
    public override string ToString() => nameof(ChangePasswordCommand);
}
