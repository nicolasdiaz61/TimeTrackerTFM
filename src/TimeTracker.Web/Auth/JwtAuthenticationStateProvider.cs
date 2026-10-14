using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Components.Authorization;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Estado de autenticación a partir del payload del JWT. La firma no se valida aquí (no se puede en el navegador):
/// solo decide qué mostrar; la API valida el token en cada petición (ADR 0012).
/// </summary>
public sealed class JwtAuthenticationStateProvider(TokenStore store, TimeProvider timeProvider) : AuthenticationStateProvider
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
            // Mal formado o caducado: se borra sin mostrar errores técnicos.
            await store.ClearAsync();
            return Anonymous;
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", "name", "role")));
    }

    public async Task SignInAsync(string token)
    {
        await store.SetAsync(token);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        await store.ClearAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private static List<Claim>? TryReadClaims(string token, DateTimeOffset now)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("exp", out var exp)
                || !exp.TryGetInt64(out var expSeconds)
                || DateTimeOffset.FromUnixTimeSeconds(expSeconds) <= now)
            {
                return null;
            }

            var claims = new List<Claim>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    claims.AddRange(property.Value.EnumerateArray().Select(item => new Claim(property.Name, ValueOf(item))));
                }
                else
                {
                    claims.Add(new Claim(property.Name, ValueOf(property.Value)));
                }
            }

            return claims;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string ValueOf(JsonElement element)
        => element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText();

    private static byte[] DecodeBase64Url(string segment)
    {
        var base64 = segment.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
