using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TimeTracker.Api.IntegrationTests;

/// <summary>
/// Fabrica JWT a mano (base64url + HMAC), sin reutilizar el emisor de producción: así una prueba de validación
/// no depende del código que valida. Vida larga por defecto para que avanzar el reloj en las pruebas de
/// fichaje no caduque el token (D8).
/// </summary>
internal static class TestTokens
{
    public const string Issuer = "timetracker-api";
    public const string Audience = "timetracker-web";

    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(30);

    public static string Create(
        Guid userId,
        string role,
        DateTimeOffset issuedAt,
        TimeSpan? lifetime = null,
        string? signingKey = null,
        string issuer = Issuer,
        string audience = Audience,
        string? subject = null,
        string name = "Usuario de prueba",
        string? email = null,
        string algorithm = "HS256",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null)
    {
        var header = new JsonObject { ["alg"] = algorithm, ["typ"] = "JWT" };
        var payload = BuildPayload(userId, role, issuedAt, lifetime, issuer, audience, subject, name, email, notBefore, expires);
        var signingInput = $"{Encode(header)}.{Encode(payload)}";
        var key = Encoding.UTF8.GetBytes(signingKey ?? ApiFactory.ValidSigningKey);
        byte[] signature = algorithm switch
        {
            "HS256" => HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(signingInput)),
            "HS512" => HMACSHA512.HashData(key, Encoding.ASCII.GetBytes(signingInput)),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Algoritmo no soportado por TestTokens."),
        };

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    /// <summary>Token con <c>alg: none</c> y sin firma (confusión de algoritmo).</summary>
    public static string CreateUnsigned(Guid userId, string role, DateTimeOffset issuedAt)
    {
        var header = new JsonObject { ["alg"] = "none", ["typ"] = "JWT" };
        var payload = BuildPayload(userId, role, issuedAt, null, Issuer, Audience, null, "Usuario de prueba", null, null, null);
        return $"{Encode(header)}.{Encode(payload)}.";
    }

    /// <summary>Cambia el payload conservando cabecera y firma originales (la firma deja de corresponder).</summary>
    public static string ReplacePayload(string token, Action<JsonObject> change)
    {
        var parts = token.Split('.');
        var payload = JsonNode.Parse(Base64Url.DecodeFromChars(parts[1]))!.AsObject();
        change(payload);
        return $"{parts[0]}.{Encode(payload)}.{parts[2]}";
    }

    private static JsonObject BuildPayload(
        Guid userId,
        string role,
        DateTimeOffset issuedAt,
        TimeSpan? lifetime,
        string issuer,
        string audience,
        string? subject,
        string name,
        string? email,
        DateTimeOffset? notBefore,
        DateTimeOffset? expires)
        => new()
        {
            ["sub"] = subject ?? userId.ToString(),
            ["name"] = name,
            ["email"] = email ?? string.Create(CultureInfo.InvariantCulture, $"{userId:N}@example.test"),
            ["role"] = role,
            ["jti"] = Guid.NewGuid().ToString(),
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["nbf"] = (notBefore ?? issuedAt).ToUnixTimeSeconds(),
            ["exp"] = (expires ?? issuedAt + (lifetime ?? DefaultLifetime)).ToUnixTimeSeconds(),
            ["iss"] = issuer,
            ["aud"] = audience,
        };

    private static string Encode(JsonObject json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json.ToJsonString()));
}
