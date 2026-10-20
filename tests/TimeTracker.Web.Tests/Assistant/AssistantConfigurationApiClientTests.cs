using System.Net;
using System.Text;
using System.Text.Json;

using TimeTracker.Web.Assistant;
using TimeTracker.Web.Http;

namespace TimeTracker.Web.Tests.Assistant;

public class AssistantConfigurationApiClientTests
{
    private const string ConfigurationJson = """
        {"provider":"OpenAI","model":"modelo-ejemplo","timeoutSeconds":30,"maxOutputTokens":1024,"effort":"medium",
         "sources":{"provider":"stored","model":"env","timeoutSeconds":"env","maxOutputTokens":"env","effort":"env","credential":"stored"},
         "credentialConfigured":true,"credentialStorageAvailable":true,
         "allowedProviders":[{"provider":"OpenAI","requiresCredential":true,"supportsEffort":true}],
         "concurrencyToken":"7","updatedAt":"2026-10-19T08:30:00+00:00","updatedBy":"Ana Ejemplo","applied":true}
        """;

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static AssistantConfigurationApiClient ClientFor(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    private static HttpResponseMessage Json(HttpStatusCode status, string body, string mediaType = "application/json")
        => new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    private static AssistantConfigurationRequestDto SaveRequest()
        => new("OpenAI", "modelo-ejemplo", null, null, null, "clave-prueba-123456", "7");

    [Fact]
    public async Task GetAsync_GetsTheConfigurationRoute_AndReadsTheContract()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ConfigurationJson));

        var result = await ClientFor(handler).GetAsync();

        Assert.Null(result.Error);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/asistente/configuracion", handler.Request.RequestUri!.ToString());
        var configuration = result.Value!;
        Assert.Equal("OpenAI", configuration.Provider);
        Assert.Equal(30, configuration.TimeoutSeconds);
        Assert.Equal("stored", configuration.Sources.Provider);
        Assert.Equal("stored", configuration.Sources.Credential);
        Assert.True(configuration.CredentialConfigured);
        Assert.True(configuration.CredentialStorageAvailable);
        Assert.Equal(new AllowedProviderDto("OpenAI", true, true), configuration.AllowedProviders.Single());
        Assert.Equal("7", configuration.ConcurrencyToken);
        Assert.Equal("Ana Ejemplo", configuration.UpdatedBy);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.Zero), configuration.UpdatedAt);
    }

    [Fact]
    public async Task SaveAsync_PutsTheRequestWithCamelCaseNamesAndNullsForOmittedFields()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ConfigurationJson));

        var result = await ClientFor(handler).SaveAsync(SaveRequest());

        Assert.Null(result.Error);
        Assert.Equal("7", result.Value!.ConcurrencyToken);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/asistente/configuracion", handler.Request.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.Body!);
        var names = body.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(["concurrencyToken", "credential", "effort", "maxOutputTokens", "model", "provider", "timeoutSeconds"], names);
        Assert.Equal("clave-prueba-123456", body.RootElement.GetProperty("credential").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("timeoutSeconds").ValueKind);
    }

    [Fact]
    public async Task SaveAsync_BadRequestWithFieldErrors_ReturnsTheMessagesPerField()
    {
        var handler = new StubHandler(_ => Json(
            HttpStatusCode.BadRequest,
            """{"title":"Solicitud no válida","status":400,"errors":{"model":["El campo model es obligatorio."],"timeoutSeconds":["Debe estar entre 5 y 55.","Otro."]}}""",
            "application/problem+json"));

        var result = await ClientFor(handler).SaveAsync(SaveRequest());

        Assert.Null(result.Value);
        Assert.Equal(HttpStatusCode.BadRequest, result.Error!.Exception.StatusCode);
        Assert.Equal(["El campo model es obligatorio."], result.Error.FieldErrors["model"]);
        Assert.Equal(["Debe estar entre 5 y 55.", "Otro."], result.Error.FieldErrors["timeoutSeconds"]);
    }

    [Fact]
    public async Task SaveAsync_BadRequestWithOnlyADetail_ReturnsTheDetailAndNoFields()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"detail":"El cuerpo de la solicitud es obligatorio."}""", "application/problem+json"));

        var result = await ClientFor(handler).SaveAsync(SaveRequest());

        Assert.Equal("El cuerpo de la solicitud es obligatorio.", result.Error!.Exception.Message);
        Assert.Empty(result.Error.FieldErrors);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("""{"errors":"texto"}""")]
    [InlineData("""{"errors":{"model":"texto"}}""")]
    public async Task SaveAsync_BadRequestWithAnUnreadableErrorsBody_ReturnsNoFieldsWithoutFailing(string body)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, body, "application/problem+json"));

        var result = await ClientFor(handler).SaveAsync(SaveRequest());

        Assert.Equal(HttpStatusCode.BadRequest, result.Error!.Exception.StatusCode);
        Assert.Empty(result.Error.FieldErrors);
    }

    [Fact]
    public async Task SaveAsync_Conflict_ReturnsTheDetail()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Conflict, """{"detail":"La configuración cambió desde que la consultó."}""", "application/problem+json"));

        var result = await ClientFor(handler).SaveAsync(SaveRequest());

        Assert.Null(result.Value);
        Assert.Equal("La configuración cambió desde que la consultó.", result.Error!.Exception.Message);
        Assert.Equal(HttpStatusCode.Conflict, result.Error.Exception.StatusCode);
        Assert.Empty(result.Error.FieldErrors);
    }

    [Fact]
    public async Task TestAsync_PostsToTheTestRoute_AndReturnsTheOutcome()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"result":"ok","message":"La conexión funciona."}"""));

        var result = await ClientFor(handler).TestAsync(new AssistantConnectionTestRequestDto("OpenAI", null, null, null, null, "clave-prueba-123456"));

        Assert.Null(result.Error);
        Assert.Equal(new AssistantConnectionTestResponseDto("ok", "La conexión funciona."), result.Value);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/asistente/configuracion/prueba", handler.Request.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(["credential", "effort", "maxOutputTokens", "model", "provider", "timeoutSeconds"], body.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public async Task TestAsync_TooManyRequests_ReturnsTheDetailAndTheRetryAfterSeconds()
    {
        var handler = new StubHandler(_ =>
        {
            var response = Json(HttpStatusCode.TooManyRequests, """{"detail":"Ha realizado demasiadas pruebas de conexión."}""", "application/problem+json");
            response.Headers.TryAddWithoutValidation("Retry-After", "42");
            return response;
        });

        var result = await ClientFor(handler).TestAsync(new AssistantConnectionTestRequestDto(null, null, null, null, null, null));

        Assert.Equal("Ha realizado demasiadas pruebas de conexión.", result.Error!.Exception.Message);
        Assert.Equal(HttpStatusCode.TooManyRequests, result.Error.Exception.StatusCode);
        Assert.Equal(42, result.Error.RetryAfterSeconds);
    }

    [Fact]
    public async Task ResetAsync_DeletesTheConfiguration_AndNoContentIsSuccess()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        var error = await ClientFor(handler).ResetAsync();

        Assert.Null(error);
        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/asistente/configuracion", handler.Request.RequestUri!.ToString());
        Assert.Null(handler.Body);
    }

    [Fact]
    public async Task ResetAsync_Conflict_ReturnsTheDetail()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Conflict, """{"detail":"La configuración cambió."}""", "application/problem+json"));

        var error = await ClientFor(handler).ResetAsync();

        Assert.Equal("La configuración cambió.", error!.Exception.Message);
        Assert.Equal(HttpStatusCode.Conflict, error.Exception.StatusCode);
    }

    [Fact]
    public async Task Forbidden_ReturnsTheGenericMessageWithoutTheServerDetail()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Forbidden, """{"detail":"Texto interno."}""", "application/problem+json"));

        var result = await ClientFor(handler).GetAsync();

        Assert.Equal(ApiRequest.GenericErrorMessage, result.Error!.Exception.Message);
        Assert.Equal(HttpStatusCode.Forbidden, result.Error.Exception.StatusCode);
    }

    [Fact]
    public async Task NetworkFailure_ReturnsTheGenericMessageWithoutStatus()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("sin red"));

        var result = await ClientFor(handler).SaveAsync(SaveRequest());

        Assert.Equal(ApiRequest.GenericErrorMessage, result.Error!.Exception.Message);
        Assert.Null(result.Error.Exception.StatusCode);
        Assert.Empty(result.Error.FieldErrors);
    }

    [Fact]
    public async Task ErrorsNeverCarryTheCredentialTheRequestSent()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"errors":{"credential":["El campo credential no es válido."]}}""", "application/problem+json"));

        var result = await ClientFor(handler).SaveAsync(SaveRequest());

        Assert.DoesNotContain("clave-prueba-123456", result.Error!.Exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-prueba-123456", string.Join(' ', result.Error.FieldErrors.Values.SelectMany(messages => messages)), StringComparison.Ordinal);
    }
}
