using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TimeTracker.Api.IntegrationTests;

public class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Sin cabecera Authorization: la salud es anónima (AH-1).
    private readonly HttpClient _client = factory.CreateAnonymousClient();

    [Fact]
    public async Task Get_ReturnsOkWithHealthyJson()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Healthy", body?["status"]);
    }

    [Fact]
    public async Task Post_WithToken_ReturnsMethodNotAllowed()
    {
        var response = await factory.CreateClient().PostAsync("/health", content: null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutToken_ReturnsUnauthorized()
    {
        // La política de respaldo autenticada se evalúa antes que el 405 (V13, AH-3).
        var response = await _client.PostAsync("/health", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_BodyHasOnlyStatusProperty()
    {
        var response = await _client.GetAsync("/health");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var propertyNames = document.RootElement.EnumerateObject().Select(property => property.Name);
        Assert.Equal(["status"], propertyNames);
    }

    [Fact]
    public async Task Get_HeadersDoNotExposeMachineNameOrConfiguration()
    {
        var response = await _client.GetAsync("/health");

        var headers = response.Headers.Concat(response.Content.Headers).ToList();
        Assert.DoesNotContain(headers, header => header.Key.Equals("Server", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(headers, header => header.Value.Any(
            value => value.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Health_WithUnreachableDatabase_ReturnsHealthy()
    {
        // La base de la fábrica (db.invalid) no existe: la salud no depende de ella y la bandera no está activa.
        var response = await _client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"status":"Healthy"}""", await response.Content.ReadAsStringAsync(CancellationToken.None));
    }
}
