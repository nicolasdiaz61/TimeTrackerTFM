using Microsoft.JSInterop;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Guarda el token de acceso en memoria y en sessionStorage (se pierde al cerrar la pestaña; ADR 0012).
/// La copia en memoria permite seguir enviándolo aunque alguien borre la clave del almacenamiento (WS-10).
/// </summary>
public sealed class TokenStore(IJSRuntime js)
{
    public const string StorageKey = "timetracker.accessToken";

    private string? token;

    public async ValueTask<string?> GetAsync()
    {
        if (token is null)
        {
            token = await js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
        }

        return token;
    }

    public async ValueTask SetAsync(string value)
    {
        token = value;
        await js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, value);
    }

    public async ValueTask ClearAsync()
    {
        token = null;
        await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
    }
}
