using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace TimeTracker.Web.Tests.Support;

/// <summary>Fábrica de JWT ficticios (firma inválida): la Web solo lee el payload.</summary>
internal static class Tokens
{
    public static string Create(DateTimeOffset expiry, string? companyName = null, bool? passwordChangePending = null, string role = "Employee")
    {
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = "u1",
            ["name"] = "Usuario Ejemplo",
            ["role"] = role,
            ["exp"] = expiry.ToUnixTimeSeconds(),
        };
        if (companyName is not null)
        {
            payload["cname"] = companyName;
        }

        if (passwordChangePending is { } pending)
        {
            payload["mcp"] = pending ? "true" : "false";
        }

        return $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url(JsonSerializer.Serialize(payload))}.firma";
    }

    public static string WithPendingChange() => Create(DateTimeOffset.UtcNow.AddMinutes(30), passwordChangePending: true);

    public static string WithoutPendingChange() => Create(DateTimeOffset.UtcNow.AddMinutes(30));

    public static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)), "jwt", "name", "role"));

    private static string Base64Url(string text)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
