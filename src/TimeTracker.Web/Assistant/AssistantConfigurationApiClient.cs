using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Assistant;

/// <summary>Resultado de una llamada con cuerpo de respuesta: el valor o el error ya traducido.</summary>
public sealed record AssistantConfigurationCallResult<T>(T? Value, AssistantConfigurationError? Error);

/// <summary>
/// Cliente tipado de /api/asistente/configuracion (solo Manager). El 401 lo trata el <c>BearerTokenHandler</c>;
/// el resto de fallos vuelve como <see cref="AssistantConfigurationError"/>, sin lanzar.
/// </summary>
public sealed class AssistantConfigurationApiClient(HttpClient http) : IDisposable
{
    private const string Route = "api/asistente/configuracion";

    private static readonly HashSet<HttpStatusCode> DetailStatuses = [HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.TooManyRequests];

    public Task<AssistantConfigurationCallResult<AssistantConfigurationDto>> GetAsync(CancellationToken cancellationToken = default)
        => CallAsync<AssistantConfigurationDto>(() => http.GetAsync(Route, cancellationToken), cancellationToken);

    public Task<AssistantConfigurationCallResult<AssistantConfigurationDto>> SaveAsync(AssistantConfigurationRequestDto request, CancellationToken cancellationToken = default)
        => CallAsync<AssistantConfigurationDto>(() => http.PutAsJsonAsync(Route, request, cancellationToken), cancellationToken);

    public Task<AssistantConfigurationCallResult<AssistantConnectionTestResponseDto>> TestAsync(AssistantConnectionTestRequestDto request, CancellationToken cancellationToken = default)
        => CallAsync<AssistantConnectionTestResponseDto>(() => http.PostAsJsonAsync($"{Route}/prueba", request, cancellationToken), cancellationToken);

    /// <summary>Borra la configuración guardada vigente; devuelve el error o <see langword="null"/> si salió bien (204).</summary>
    public async Task<AssistantConfigurationError?> ResetAsync(CancellationToken cancellationToken = default)
    {
        var capture = new Capture();
        try
        {
            await ApiRequest.SendAsync(() => capture.SendAsync(http.DeleteAsync(Route, cancellationToken), cancellationToken), DetailStatuses, cancellationToken);
            return null;
        }
        catch (ApiRequestException exception)
        {
            return capture.ToError(exception);
        }
    }

    public void Dispose() => http.Dispose();

    private static async Task<AssistantConfigurationCallResult<T>> CallAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        var capture = new Capture();
        try
        {
            var value = await ApiRequest.SendAsync<T>(() => capture.SendAsync(send(), cancellationToken), DetailStatuses, cancellationToken);
            return new AssistantConfigurationCallResult<T>(value, null);
        }
        catch (ApiRequestException exception)
        {
            return new AssistantConfigurationCallResult<T>(default, capture.ToError(exception));
        }
    }

    // ApiRequest solo conserva el detalle: los errores por campo del 400 y el Retry-After se leen aquí, antes de que
    // se traduzca la respuesta (el contenido queda en memoria y puede leerse otra vez).
    private sealed class Capture
    {
        private Dictionary<string, string[]> fieldErrors = [];
        private int? retryAfterSeconds;

        public async Task<HttpResponseMessage> SendAsync(Task<HttpResponseMessage> pending, CancellationToken cancellationToken)
        {
            var response = await pending;
            retryAfterSeconds = response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero ? (int)Math.Ceiling(delta.TotalSeconds) : null;
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                fieldErrors = await ReadFieldErrorsAsync(response, cancellationToken);
            }

            return response;
        }

        public AssistantConfigurationError ToError(ApiRequestException exception) => new(exception, fieldErrors, retryAfterSeconds);

        private static async Task<Dictionary<string, string[]>> ReadFieldErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var result = new Dictionary<string, string[]>();
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject().Where(field => field.Value.ValueKind == JsonValueKind.Array))
                    {
                        result[field.Name] = [.. field.Value.EnumerateArray().Where(message => message.ValueKind == JsonValueKind.String).Select(message => message.GetString()!)];
                    }
                }

                return result;
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
