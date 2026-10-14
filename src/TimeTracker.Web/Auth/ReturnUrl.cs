namespace TimeTracker.Web.Auth;

/// <summary>Valida el destino posterior al inicio de sesión: solo rutas relativas de la propia aplicación (redirección abierta).</summary>
public static class ReturnUrl
{
    public const string DefaultTarget = "/fichajes";

    private const char Backslash = (char)92;

    public static bool IsSafe(string? value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '/')
        {
            return false;
        }

        if (value.Length > 1 && value[1] is '/' or Backslash)
        {
            return false;
        }

        // Tabuladores y saltos de línea los ignoran los navegadores y podrían convertir «/<TAB>/x» en «//x».
        return !value.Any(char.IsControl);
    }
}
