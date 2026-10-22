namespace TimeTracker.Web.Assistant;

/// <summary>
/// Avisos de alcance de /asistente/configuracion (DD-18). El nombre de la compañía sale de la sesión (claim <c>cname</c>) y se
/// devuelve tal cual: la página lo pinta como texto plano, de modo que un nombre con marcado se ve literalmente. Nunca se
/// afirma que la configuración valga para toda la instancia ni se menciona un identificador de compañía.
/// </summary>
public static class AssistantConfigurationNotice
{
    /// <summary>«Este cambio afecta a todas las personas de la compañía {nombre}».</summary>
    public static string Scope(string? companyName)
        => $"Este cambio afecta a todas las personas de {CompanyPhrase(companyName)}.";

    /// <summary>Advertencia de la confirmación de restablecimiento: borra la configuración vigente, aunque sea ajena.</summary>
    public static string ResetWarning(string? companyName)
        => $"Se borrará la configuración guardada vigente de {CompanyPhrase(companyName)}, incluida la clave, aunque otro responsable la haya cambiado "
            + "después de que usted abriera esta página. El asistente volverá a usar los valores del entorno.";

    private static string CompanyPhrase(string? companyName)
        => string.IsNullOrWhiteSpace(companyName) ? "su compañía" : $"la compañía {companyName}";
}
