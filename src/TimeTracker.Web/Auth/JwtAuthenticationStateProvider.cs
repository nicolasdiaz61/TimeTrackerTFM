using System.Globalization;
using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Estado de autenticación a partir del payload del JWT. La firma no se valida aquí (no se puede en el navegador):
/// solo decide qué mostrar; la API valida el token en cada petición (ADR 0012).
/// </summary>
public sealed class JwtAuthenticationStateProvider(TokenStore store, TimeProvider timeProvider, SessionNotice notice) : AuthenticationStateProvider, ISessionSignIn
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await store.GetAsync();
        if (string.IsNullOrEmpty(token))
        {
            return Anonymous;
        }

        var claims = TryReadClaims(token, timeProvider.GetUtcNow());
        if (claims is null)
        {
            // Mal formado o caducado: se borra sin errores técnicos y /login avisa de que la sesión terminó.
            await store.ClearAsync();
            notice.MarkExpired();
            return Anonymous;
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", "name", "role")));
    }

    public async Task SignInAsync(string token)
    {
        await store.SetAsync(token);
        notice.Clear();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        await store.ClearAsync();
        notice.Clear();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    // Vigente: con «exp» entero y posterior al instante actual; cualquier otra cosa se trata como no autenticado.
    private static List<Claim>? TryReadClaims(string token, DateTimeOffset now)
    {
        var claims = JwtPayload.TryReadClaims(token);
        var exp = claims?.FirstOrDefault(claim => claim.Type == "exp")?.Value;
        return claims is not null
            && long.TryParse(exp, NumberStyles.None, CultureInfo.InvariantCulture, out var expSeconds)
            && IsAfter(expSeconds, now)
            ? claims
            : null;
    }

    private static bool IsAfter(long expSeconds, DateTimeOffset now)
        => expSeconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds() && DateTimeOffset.FromUnixTimeSeconds(expSeconds) > now;
}
