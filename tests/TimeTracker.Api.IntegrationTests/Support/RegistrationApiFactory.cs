using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>
/// API con PostgreSQL real y el registro de compañías abierto (<c>Registration:Enabled=true</c>) con códigos de invitación inventados.
/// Captura todo lo que registra la aplicación para comprobar que ningún dato personal ni secreto llega a los registros.
/// </summary>
internal sealed class RegistrationApiFactory(
    string connectionString,
    string? inviteCodes = null,
    IReadOnlyDictionary<string, string?>? settings = null) : ApiFactory
{
    public const string RegisterPath = "/api/auth/register-company";
    public const string Code = "codigo-prueba-0123456789";
    public const string SecondCode = "otro-codigo-prueba-9876543210";
    public const string Password = "contrasena-prueba-12345";

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Registration:Enabled", "true");
        builder.UseSetting("Registration:InviteCodes", inviteCodes ?? $"{Code},{SecondCode}");
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }

    /// <summary>Cuerpo completo y válido; cada argumento se puede sustituir (o anular con <c>null</c>) para probar un campo.</summary>
    public static Dictionary<string, object?> Body(
        string? companyName = null,
        string? managerName = "Ana Gómez",
        string? email = null,
        string? password = Password,
        string? inviteCode = Code)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new Dictionary<string, object?>
        {
            ["companyName"] = companyName ?? $"Acme {unique}",
            ["managerName"] = managerName,
            ["email"] = email ?? $"ana.{unique}@example.test",
            ["password"] = password,
            ["inviteCode"] = inviteCode,
        };
    }

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, object body)
        => client.PostAsJsonAsync(RegisterPath, body, CancellationToken.None);

    public static Task<HttpResponseMessage> PostRawAsync(HttpClient client, string? json)
    {
        // Sin cuerpo de verdad (ni contenido ni tipo de contenido), como un POST vacío de curl.
        HttpContent? content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json");
        return client.PostAsync(RegisterPath, content, CancellationToken.None);
    }

    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));

    /// <summary>Contenido del token sin verificar la firma (las pruebas de validación viven en otro sitio).</summary>
    public static JsonElement TokenPayload(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.Clone();
    }
}
