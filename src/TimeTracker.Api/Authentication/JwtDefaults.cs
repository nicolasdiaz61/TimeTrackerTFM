namespace TimeTracker.Api.Authentication;

/// <summary>Valores fijos del token: no son secretos ni varían por entorno, así que viven en código (DD-9, DD-12).</summary>
public static class JwtDefaults
{
    public const string Issuer = "timetracker-api";

    public const string Audience = "timetracker-web";

    /// <summary>Identificador de la compañía del usuario. Nunca se acepta del cliente; sale solo de aquí (DD-6).</summary>
    public const string CompanyClaim = "cid";

    /// <summary>Nombre visible de la compañía. Solo presentación: no interviene en ninguna decisión de autorización (DD-6).</summary>
    public const string CompanyNameClaim = "cname";

    /// <summary>Presente, con el valor <see cref="PasswordChangePendingValue"/>, solo si el usuario debe cambiar su contraseña.</summary>
    public const string PasswordChangePendingClaim = "mcp";

    public const string PasswordChangePendingValue = "true";

    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);
}
