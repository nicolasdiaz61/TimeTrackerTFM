using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Application.Authentication;

/// <summary>
/// Política de contraseñas nuevas (DD-20): de 12 a 128 caracteres, sin reglas de composición y sin recortar ni normalizar los
/// espacios. El inicio de sesión no la aplica: solo la usan el registro, el alta de personas y el cambio de contraseña.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;

    // El mismo tope que el inicio de sesión: evita hashear cadenas enormes (DoS, API4).
    public const int MaximumLength = LoginUseCase.MaxPasswordLength;

    /// <summary>Devuelve el error del campo indicado, o <c>null</c> si la contraseña es válida. El mensaje nunca repite la contraseña.</summary>
    public static FieldError? Validate(string? password, string fieldName)
        => password is { Length: >= MinimumLength and <= MaximumLength }
            ? null
            : new FieldError(fieldName, $"La contraseña debe tener entre {MinimumLength} y {MaximumLength} caracteres.");
}
