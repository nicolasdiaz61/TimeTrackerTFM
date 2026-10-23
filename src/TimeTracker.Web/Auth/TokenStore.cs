namespace TimeTracker.Web.Auth;

/// <summary>
/// Guarda el token de acceso en memoria y en el almacenamiento local del navegador, que comparten todas las pestañas (ADR 0028,
/// que sustituye el sessionStorage del ADR 0012). La copia en memoria permite seguir enviándolo aunque alguien borre la clave del
/// almacenamiento (WS-10) o el navegador lo bloquee (modo privado): entonces la sesión vive solo en esa pestaña.
/// </summary>
public sealed class TokenStore(ISessionStorage storage) : IAsyncDisposable
{
    public const string StorageKey = "timetracker.accessToken";

    private string? token;
    private bool legacyChecked;
    private Func<string?, string?, Task>? onExternalChange;

    public async ValueTask<string?> GetAsync()
    {
        if (token is null)
        {
            token = await ReadAsync();
        }

        return token;
    }

    public async ValueTask SetAsync(string value)
    {
        token = value;
        await TryAsync(() => storage.SetAsync(StorageKey, value));
    }

    public async ValueTask ClearAsync()
    {
        token = null;
        await TryAsync(() => storage.RemoveAsync(StorageKey));
    }

    /// <summary>
    /// Avisa (token anterior, token actual) cuando OTRA pestaña inicia, cierra o renueva la sesión. La copia en memoria ya viene
    /// actualizada y nunca se vuelve a escribir en el almacenamiento, de modo que dos pestañas no se reenvían el cambio.
    /// </summary>
    public async ValueTask StartListeningAsync(Func<string?, string?, Task> onChange)
    {
        onExternalChange = onChange;
        await TryAsync(() => storage.ListenAsync(HandleExternalChangeAsync));
    }

    public async ValueTask DisposeAsync()
    {
        onExternalChange = null;
        await TryAsync(storage.StopListeningAsync);
    }

    private async Task HandleExternalChangeAsync(string? key, string? newValue)
    {
        if (key is not null && key != StorageKey)
        {
            return;
        }

        var previous = token;
        token = string.IsNullOrEmpty(newValue) ? null : newValue;
        if (onExternalChange is { } callback)
        {
            await callback(previous, token);
        }
    }

    // Lee el token compartido; la primera vez migra la sesión de la versión anterior (solo de pestaña) y la borra.
    private async Task<string?> ReadAsync()
    {
        try
        {
            string? legacy = null;
            if (!legacyChecked)
            {
                legacy = await storage.TakeLegacyAsync(StorageKey);
                legacyChecked = true;
            }

            var current = await storage.GetAsync(StorageKey);
            if (string.IsNullOrEmpty(current) && !string.IsNullOrEmpty(legacy))
            {
                await storage.SetAsync(StorageKey, legacy);
                return legacy;
            }

            return string.IsNullOrEmpty(current) ? null : current;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Almacenamiento bloqueado: la sesión queda solo en memoria y la aplicación no se cae.
            return null;
        }
    }

    private static async Task TryAsync(Func<ValueTask> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Almacenamiento bloqueado o escucha no disponible: se degrada a la copia en memoria.
        }
    }
}
