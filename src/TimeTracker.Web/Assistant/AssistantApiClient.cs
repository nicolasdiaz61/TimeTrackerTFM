using System.Net;
using System.Net.Http.Json;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Assistant;

/// <summary>Resultado de una consulta: la respuesta o el error ya traducido, con el <c>Retry-After</c> del 429 si llegó.</summary>
public sealed record AssistantCallResult(string? Reply, ApiRequestException? Error, int? RetryAfterSeconds);

/// <summary>Cliente tipado de POST /api/asistente. El 401 lo trata el <c>BearerTokenHandler</c> (cierra la sesión y navega a /login).</summary>
public sealed class AssistantApiClient(HttpClient http) : IDisposable
{
    private static readonly HashSet<HttpStatusCode> DetailStatuses = [HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable];

    public async Task<AssistantCallResult> AskAsync(AssistantRequestDto request, CancellationToken cancellationToken = default)
    {
        int? retryAfterSeconds = null;
        try
        {
            var response = await ApiRequest.SendAsync<AssistantResponseDto>(
                async () =>
                {
                    var message = await http.PostAsJsonAsync("api/asistente", request, cancellationToken);
                    retryAfterSeconds = ReadRetryAfter(message);
                    return message;
                },
                DetailStatuses,
                cancellationToken);
            return new AssistantCallResult(response.Reply, null, null);
        }
        catch (ApiRequestException exception)
        {
            return new AssistantCallResult(null, exception, retryAfterSeconds);
        }
    }

    public void Dispose() => http.Dispose();

    // Solo la forma en segundos (la que emite la API); una fecha HTTP se ignora.
    private static int? ReadRetryAfter(HttpResponseMessage response)
        => response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero ? (int)Math.Ceiling(delta.TotalSeconds) : null;
}
