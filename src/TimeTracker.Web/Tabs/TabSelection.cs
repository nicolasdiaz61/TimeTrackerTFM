namespace TimeTracker.Web.Tabs;

/// <summary>
/// Reglas de las pestañas de una página: qué pestaña muestra un parámetro <c>?tab=</c> (con valor por defecto si falta o no existe)
/// y a cuál pasa el foco con las flechas. Sin dependencia del navegador.
/// </summary>
public static class TabSelection
{
    public const string QueryParameter = "tab";

    public const string ArrowLeft = "ArrowLeft";

    public const string ArrowRight = "ArrowRight";

    /// <summary>Clave de la pestaña pedida, o la primera si el valor es nulo, vacío o desconocido.</summary>
    public static string Resolve(string? requested, IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return keys.FirstOrDefault(key => string.Equals(key, requested, StringComparison.OrdinalIgnoreCase)) ?? keys[0];
    }

    /// <summary>Pestaña a la que mueven las flechas (circular); la misma si la tecla no es una flecha horizontal.</summary>
    public static string Move(string current, IReadOnlyList<string> keys, string key)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var index = Math.Max(0, keys.ToList().IndexOf(current));
        return key switch
        {
            ArrowRight => keys[(index + 1) % keys.Count],
            ArrowLeft => keys[(index - 1 + keys.Count) % keys.Count],
            _ => current,
        };
    }
}
