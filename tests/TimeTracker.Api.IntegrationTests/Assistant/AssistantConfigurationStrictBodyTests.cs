using System.Net;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// V-7 y DD-14: el cuerpo solo admite los campos del contrato. Una propiedad desconocida (<c>url</c>, <c>endpoint</c>...) da 400 y
/// no 500, y no se lee ni se guarda nada ni se contacta al proveedor (sin campo de URL no hay SSRF).
/// </summary>
public sealed class AssistantConfigurationStrictBodyTests
{
    [Theory]
    [InlineData("url")]
    [InlineData("endpoint")]
    [InlineData("baseUrl")]
    public async Task Put_WithAnUnknownProperty_Returns400AndSavesNothing(string property)
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateClient().PutJsonAsync(
            $$"""{"provider":"Ollama","model":"modelo-x","concurrencyToken":"0","{{property}}":"https://evil.example.test/"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("evil.example.test", await response.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
        Assert.Equal((0, 0), (store.GetCalls, store.WriteCalls));
    }

    [Theory]
    [InlineData("url")]
    [InlineData("endpoint")]
    public async Task ConnectionTest_WithAnUnknownProperty_Returns400WithoutContactingTheProvider(string property)
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateClient().PostTestAsync($$"""{"provider":"Ollama","{{property}}":"https://evil.example.test/"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Tester.Candidates);
        Assert.Equal((0, 0), (store.GetCalls, store.WriteCalls));
    }

    // Desviación aceptada de DD-14 (ver design.md): los campos numéricos son int?, así que un valor que no es un entero JSON
    // da el 400 general de formato y no un mapa de errores por campo. Esta prueba fija ese comportamiento para que un cambio sea consciente.
    [Theory]
    [InlineData("\"timeoutSeconds\":\"abc-no-numerico\"")]
    [InlineData("\"timeoutSeconds\":1.5")]
    [InlineData("\"maxOutputTokens\":99999999999")]
    [InlineData("\"maxOutputTokens\":true")]
    public async Task Put_WithANonIntegerNumericField_Returns400WithoutPerFieldErrors_AndSavesNothing(string field)
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateClient().PutJsonAsync($$"""{"provider":"Ollama","model":"modelo-x","concurrencyToken":"0",{{field}}}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain("abc-no-numerico", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"errors\"", text, StringComparison.Ordinal);
        Assert.Equal((0, 0), (store.GetCalls, store.WriteCalls));
    }

    [Fact]
    public async Task Put_WithMalformedJson_Returns400AndSavesNothing()
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateClient().PutJsonAsync("""{"provider":""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((0, 0), (store.GetCalls, store.WriteCalls));
    }
}
