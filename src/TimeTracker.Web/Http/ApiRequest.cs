using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TimeTracker.Web.Http;

/// <summary>Envío común de los clientes tipados: lectura del ProblemDetails, mensaje genérico y estado HTTP en la excepción.</summary>
public static class ApiRequest
{
    public const string GenericErrorMessage = "No se pudo completar la operación. Inténtelo de nuevo más tarde.";

    private static readonly IReadOnlySet<HttpStatusCode> ConflictOnly = new HashSet<HttpStatusCode> { HttpStatusCode.Conflict };

    // Solo el 409 trae un mensaje de negocio en español; cualquier otro fallo es genérico.
    public static Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
        => SendAsync<T>(send, ConflictOnly, cancellationToken);

    /// <summary>Como la sobrecarga básica, pero el <c>detail</c> en español de la API solo se reproduce para los estados indicados (DD-18).</summary>
    public static async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send, IReadOnlySet<HttpStatusCode> detailStatuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(detailStatuses);

        try
        {
            using var response = await send();
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new ApiRequestException(GenericErrorMessage);
            }

            var detail = detailStatuses.Contains(response.StatusCode) ? await ReadDetailAsync(response, cancellationToken) : null;
            throw new ApiRequestException(string.IsNullOrWhiteSpace(detail) ? GenericErrorMessage : detail, response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Red caída, tiempo agotado, CORS rechazado o cuerpo ilegible: sin trazas ni datos técnicos para el usuario.
            throw new ApiRequestException(GenericErrorMessage);
        }
    }

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemDto>(cancellationToken);
            return problem?.Detail;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
