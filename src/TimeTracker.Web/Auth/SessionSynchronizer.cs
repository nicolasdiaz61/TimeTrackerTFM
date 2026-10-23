using Microsoft.AspNetCore.Components;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Mantiene la misma sesión en todas las pestañas del navegador (ADR 0028): si otra pestaña cierra la sesión, esta vuelve a /login;
/// si otra inicia sesión mientras esta espera en /login, continúa; si otra renueva el token (cambio de contraseña), lo toma.
/// Solo reacciona a cambios ajenos, nunca escribe en el almacenamiento, así que no hay bucles.
/// </summary>
public sealed class SessionSynchronizer(TokenStore store, JwtAuthenticationStateProvider authentication, NavigationManager navigation) : IAsyncDisposable
{
    private const string LoginPath = "login";

    private bool started;

    public async Task StartAsync()
    {
        if (started)
        {
            return;
        }

        started = true;
        await store.StartListeningAsync(OnExternalChangeAsync);
    }

    public async ValueTask DisposeAsync()
    {
        started = false;
        await store.DisposeAsync();
    }

    private async Task OnExternalChangeAsync(string? previous, string? current)
    {
        if (previous == current)
        {
            return;
        }

        var state = await authentication.RefreshAsync();
        var signedIn = state.User.Identity?.IsAuthenticated == true;
        var onLogin = IsOnLogin();

        if (!signedIn && !string.IsNullOrEmpty(previous) && !onLogin)
        {
            navigation.NavigateTo(LoginPath);
        }
        else if (signedIn && string.IsNullOrEmpty(previous) && onLogin)
        {
            navigation.NavigateTo(ReturnUrl.DefaultTarget);
        }
    }

    private bool IsOnLogin()
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        var end = relative.IndexOfAny(['?', '#']);
        var path = (end < 0 ? relative : relative[..end]).TrimEnd('/');
        return string.Equals(path, LoginPath, StringComparison.OrdinalIgnoreCase);
    }
}
