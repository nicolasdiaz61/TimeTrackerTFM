namespace TimeTracker.Web.Auth;

/// <summary>
/// Almacenamiento del navegador compartido entre pestañas (ADR 0028) y aviso de los cambios que hacen las demás.
/// Cualquier operación puede lanzar si el navegador bloquea el almacenamiento; <see cref="TokenStore"/> lo tolera.
/// </summary>
public interface ISessionStorage
{
    ValueTask<string?> GetAsync(string key);

    ValueTask SetAsync(string key, string value);

    ValueTask RemoveAsync(string key);

    /// <summary>Lee y borra el valor que la versión anterior guardaba solo para la pestaña (ADR 0012), para migrarlo una vez.</summary>
    ValueTask<string?> TakeLegacyAsync(string key);

    /// <summary>
    /// Empieza a avisar de los cambios hechos desde OTRAS pestañas (clave y nuevo valor; clave nula = se vació el almacenamiento).
    /// El navegador nunca avisa a la pestaña que escribe, así que no hay bucles.
    /// </summary>
    ValueTask ListenAsync(Func<string?, string?, Task> onChanged);

    ValueTask StopListeningAsync();
}
