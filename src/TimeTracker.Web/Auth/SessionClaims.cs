using System.Security.Claims;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Claims de presentación de la sesión: el nombre de la compañía (<c>cname</c>) y el cambio de contraseña pendiente (<c>mcp</c>).
/// La Web nunca lee ni muestra el identificador de compañía (<c>cid</c>) y el nombre no interviene en ninguna decisión.
/// </summary>
public static class SessionClaims
{
    public const string CompanyNameClaim = "cname";
    public const string PasswordChangePendingClaim = "mcp";

    public static string? CompanyName(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.FindFirst(CompanyNameClaim)?.Value;
    }

    /// <summary>Solo el valor exacto <c>true</c> cuenta, igual que en el servidor.</summary>
    public static bool IsPasswordChangePending(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return string.Equals(user.FindFirst(PasswordChangePendingClaim)?.Value, "true", StringComparison.Ordinal);
    }

    public static bool IsPasswordChangePending(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return JwtPayload.TryReadClaims(token) is { } claims
            && claims.Any(claim => claim.Type == PasswordChangePendingClaim && string.Equals(claim.Value, "true", StringComparison.Ordinal));
    }
}
