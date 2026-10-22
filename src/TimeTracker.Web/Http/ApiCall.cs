using System.Net;
using System.Text.Json;

namespace TimeTracker.Web.Http;

/// <summary>Fallo de una llamada: el error ya traducido, los mensajes por campo del 400 y el <c>Retry-After</c> del 429.</summary>
public sealed record ApiFailure(ApiRequestException Exception, IReadOnlyDictionary<string, string[]> FieldErrors, int? RetryAfterSeconds)
{
    public HttpStatusCode? StatusCode => Exception.StatusCode;

    public string Message => Exception.Message;
}

/// <summary>Resultado de una llamada con cuerpo de respuesta: el valor o el fallo ya traducido.</summary>
public sealed record ApiCallResult<T>(T? Value, ApiFailure? Error);

/// <summary>
/// Envío común de los clientes tipados que devuelven el fallo en lugar de lanzarlo: conserva los errores por campo
/// del 400 y el <c>Retry-After</c> del 429, que <see cref="ApiRequest"/> no conserva.
/// </summary>
public static class ApiCall
{
    private static readonly IReadOnlySet<HttpStatusCode> ConflictOnly = new HashSet<HttpStatusCode> { HttpStatusCode.Conflict };

    /// <summary>Solo el <c>detail</c> del 409 se reproduce al usuario; el resto de fallos es genérico.</summary>
    public static Task<ApiCallResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
        => SendAsync<T>(send, ConflictOnly, cancellationToken);

    public static async Task<ApiCallResult<T>> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        IReadOnlySet<HttpStatusCode> detailStatuses,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);

        var capture = new Capture();
        try
        {
            var value = await ApiRequest.SendAsync<T>(() => capture.SendAsync(send(), cancellationToken), detailStatuses, cancellationToken);
            return new ApiCallResult<T>(value, null);
        }
        catch (ApiRequestException exception)
        {
            return new ApiCallResult<T>(default, capture.ToFailure(exception));
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

        public ApiFailure ToFailure(ApiRequestException exception) => new(exception, fieldErrors, retryAfterSeconds);

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
