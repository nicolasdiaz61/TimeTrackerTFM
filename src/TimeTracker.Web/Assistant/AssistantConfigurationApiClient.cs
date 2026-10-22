using System.Net;
using System.Net.Http.Json;

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
        => ToError(await ApiCall.SendAsync(() => http.DeleteAsync(Route, cancellationToken), DetailStatuses, cancellationToken));

    public void Dispose() => http.Dispose();

    private static async Task<AssistantConfigurationCallResult<T>> CallAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        var result = await ApiCall.SendAsync<T>(send, DetailStatuses, cancellationToken);
        return new AssistantConfigurationCallResult<T>(result.Value, ToError(result.Error));
    }

    private static AssistantConfigurationError? ToError(ApiFailure? failure)
        => failure is null ? null : new AssistantConfigurationError(failure.Exception, failure.FieldErrors, failure.RetryAfterSeconds);
}
