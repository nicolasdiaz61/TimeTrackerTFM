using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>Rutas, cuerpos y utilidades comunes de las pruebas del alta de personas, la regeneración y el cambio de contraseña.</summary>
internal static class EmployeesHttp
{
    public const string EmployeesPath = "/api/employees";
    public const string ChangePasswordPath = "/api/auth/change-password";
    public const string LoginPath = "/api/auth/login";
    public const string TeamMembersPath = "/api/team/members";

    public static string TemporaryPasswordPath(Guid memberId) => $"{EmployeesPath}/{memberId}/temporary-password";

    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, object body)
        => client.PostAsJsonAsync(EmployeesPath, body, CancellationToken.None);

    public static Task<HttpResponseMessage> RegenerateAsync(HttpClient client, Guid memberId)
        => client.PostAsync(TemporaryPasswordPath(memberId), null, CancellationToken.None);

    public static Task<HttpResponseMessage> ChangePasswordAsync(HttpClient client, string? current, string? next, object? extra = null)
    {
        var body = new JsonObject { ["currentPassword"] = current, ["newPassword"] = next };
        if (extra is not null)
        {
            foreach (var property in JsonSerializer.SerializeToNode(extra)!.AsObject())
            {
                body[property.Key] = property.Value?.DeepClone();
            }
        }

        return client.PostAsync(ChangePasswordPath, new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"), CancellationToken.None);
    }

    public static Task<HttpResponseMessage> LoginAsync(ApiFactory factory, string email, string password)
    {
        // Sin `using`: la respuesta se lee después y el cliente de pruebas no se puede desechar antes.
        var anonymous = factory.CreateAnonymousClient();
        return anonymous.PostAsJsonAsync(LoginPath, new { email, password }, CancellationToken.None);
    }

    /// <summary>Cliente anónimo de la fábrica con el token indicado en la cabecera.</summary>
    public static HttpClient WithToken(ApiFactory factory, string accessToken)
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));

    /// <summary>Inicia sesión y devuelve el cuerpo; falla si no es un 200.</summary>
    public static async Task<JsonElement> LoginOkAsync(ApiFactory factory, string email, string password)
    {
        var response = await LoginAsync(factory, email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadAsync(response);
        return body.RootElement.Clone();
    }

    /// <summary>Estado, tipo de medio y cuerpo sin <c>traceId</c>: dos respuestas «idénticas» según la spec tienen la misma huella.</summary>
    public static async Task<string> FingerprintAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var body = JsonNode.Parse(text)!.AsObject();
        body.Remove("traceId");
        return $"{(int)response.StatusCode}|{response.Content.Headers.ContentType?.MediaType}|{body.ToJsonString()}";
    }

    /// <summary>Petición con cabecera <c>Origin</c>, para comprobar que las respuestas de error conservan CORS.</summary>
    public static Task<HttpResponseMessage> SendWithOriginAsync(HttpClient client, HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add("Origin", ApiFactory.ValidOrigin);
        return client.SendAsync(request, CancellationToken.None);
    }

    public static async Task SetMustChangePasswordAsync(string connectionString, Guid userId, bool value)
        => await MigrationDatabase.ExecuteAsync(connectionString, $"UPDATE users SET must_change_password = {(value ? "true" : "false")} WHERE id = '{userId}'");

    public static async Task<string> HashOfAsync(string connectionString, Guid userId)
        => Assert.Single(await MigrationDatabase.QueryAsync(connectionString, $"SELECT password_hash FROM users WHERE id = '{userId}'"));

    public static async Task<long> CountUsersAsync(string connectionString, Guid companyId)
        => long.Parse(Assert.Single(await MigrationDatabase.QueryAsync(connectionString, $"SELECT count(*)::text FROM users WHERE company_id = '{companyId}'")), System.Globalization.CultureInfo.InvariantCulture);
}
