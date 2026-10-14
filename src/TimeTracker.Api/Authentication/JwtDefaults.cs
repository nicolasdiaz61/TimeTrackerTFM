namespace TimeTracker.Api.Authentication;

/// <summary>Valores fijos del token: no son secretos ni varían por entorno, así que viven en código (DD-9, DD-12).</summary>
public static class JwtDefaults
{
    public const string Issuer = "timetracker-api";

    public const string Audience = "timetracker-web";

    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);
}
