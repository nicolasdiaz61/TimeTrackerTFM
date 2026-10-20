using System.Text;
using System.Text.Json;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>Rutas y utilidades de las pruebas de los endpoints de configuración del asistente.</summary>
internal static class AssistantConfigurationHttp
{
    public const string Route = "/api/asistente/configuracion";
    public const string TestRoute = "/api/asistente/configuracion/prueba";
    public const string Key = "clave-prueba-123456";

    public static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string json)
        => client.PutAsync(Route, Json(json), CancellationToken.None);

    public static Task<HttpResponseMessage> PostTestAsync(this HttpClient client, string json = "{}")
        => client.PostAsync(TestRoute, Json(json), CancellationToken.None);

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));

    /// <summary>Errores por campo de un 400 de validación (<c>errors</c>: campo → mensajes).</summary>
    public static async Task<Dictionary<string, string[]>> FieldErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("errors").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.EnumerateArray().Select(message => message.GetString()!).ToArray());
    }
}
