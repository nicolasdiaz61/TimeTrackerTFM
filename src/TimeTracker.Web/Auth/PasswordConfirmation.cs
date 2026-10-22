namespace TimeTracker.Web.Auth;

/// <summary>Coincidencia de la confirmación de contraseña: solo presentación; la política de contraseñas la impone la API.</summary>
public static class PasswordConfirmation
{
    public const string Field = "confirmPassword";
    public const string MismatchMessage = "Las contraseñas no coinciden.";

    public static bool Matches(string password, string confirmation) => string.Equals(password, confirmation, StringComparison.Ordinal);
}
