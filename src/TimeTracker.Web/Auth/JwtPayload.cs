using System.Security.Claims;
using System.Text.Json;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Lectura del payload de un JWT sin validar la firma (no se puede en el navegador): solo decide qué mostrar; la API valida
/// el token en cada petición (ADR 0012). Cada propiedad del payload es un claim; los arrays dan un claim por elemento.
/// </summary>
public static class JwtPayload
{
    public static List<Claim>? TryReadClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
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
