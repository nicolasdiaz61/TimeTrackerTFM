namespace TimeTracker.Api.Authentication;

/// <summary>
/// Las dos contraseñas del cambio y nada más (asignación masiva, API3): las propiedades desconocidas (<c>userId</c>,
/// <c>companyId</c>) se ignoran; la persona es siempre la del token.
/// </summary>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword)
{
    // El registro de un record imprime todas sus propiedades: nunca las contraseñas.
    public override string ToString() => nameof(ChangePasswordRequest);
}
