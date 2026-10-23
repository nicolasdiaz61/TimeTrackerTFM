using System.Net;
using System.Net.Http.Json;
using System.Text;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.WorkSchedules;

public interface IWorkSchedulesApi
{
    Task<ApiCallResult<WorkScheduleListDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ApiCallResult<WorkScheduleDto>> CreateAsync(WorkScheduleRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiCallResult<WorkScheduleDto>> UpdateAsync(Guid id, WorkScheduleRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Devuelve el fallo o <see langword="null"/> si salió bien.</summary>
    Task<ApiFailure?> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Asigna la jornada a una persona del equipo; <see langword="null"/> la retira. Devuelve el fallo o <see langword="null"/>.</summary>
    Task<ApiFailure?> AssignAsync(Guid memberId, Guid? workScheduleId, CancellationToken cancellationToken = default);

    /// <summary>La jornada de la persona autenticada; <c>Value</c> nulo (sin error) si no tiene ninguna.</summary>
    Task<ApiCallResult<WorkScheduleDto?>> MineAsync(CancellationToken cancellationToken = default);
}

/// <summary>Cliente tipado de /api/work-schedules y de la asignación bajo /api/team. El 401 lo trata el <c>BearerTokenHandler</c>.</summary>
public sealed class WorkSchedulesApiClient(HttpClient http) : IWorkSchedulesApi, IDisposable
{
    // Solo el detail del 409 es un mensaje de negocio en español (duplicado, tope, jornada asignada).
    private static readonly IReadOnlySet<HttpStatusCode> ConflictOnly = new HashSet<HttpStatusCode> { HttpStatusCode.Conflict };

    public Task<ApiCallResult<WorkScheduleListDto>> ListAsync(CancellationToken cancellationToken = default)
        => ApiCall.SendAsync<WorkScheduleListDto>(() => http.GetAsync("api/work-schedules", cancellationToken), cancellationToken);

    public Task<ApiCallResult<WorkScheduleDto>> CreateAsync(WorkScheduleRequestDto request, CancellationToken cancellationToken = default)
        => ApiCall.SendAsync<WorkScheduleDto>(() => http.PostAsJsonAsync("api/work-schedules", request, cancellationToken), cancellationToken);

    public Task<ApiCallResult<WorkScheduleDto>> UpdateAsync(Guid id, WorkScheduleRequestDto request, CancellationToken cancellationToken = default)
        => ApiCall.SendAsync<WorkScheduleDto>(
            () => http.PutAsJsonAsync(new Uri($"api/work-schedules/{id:D}", UriKind.Relative), request, cancellationToken),
            cancellationToken);

    public Task<ApiFailure?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => ApiCall.SendAsync(() => http.DeleteAsync(new Uri($"api/work-schedules/{id:D}", UriKind.Relative), cancellationToken), ConflictOnly, cancellationToken);

    public Task<ApiFailure?> AssignAsync(Guid memberId, Guid? workScheduleId, CancellationToken cancellationToken = default)
        => ApiCall.SendAsync(
            () => http.PutAsJsonAsync(
                new Uri($"api/team/members/{memberId:D}/work-schedule", UriKind.Relative),
                new AssignWorkScheduleRequestDto(workScheduleId),
                cancellationToken),
            ConflictOnly,
            cancellationToken);

    public async Task<ApiCallResult<WorkScheduleDto?>> MineAsync(CancellationToken cancellationToken = default)
    {
        // El 204 («sin jornada») no tiene cuerpo: se sustituye por un objeto vacío para reutilizar el envío común y se traduce a nulo.
        var withoutSchedule = false;
        var result = await ApiCall.SendAsync<WorkScheduleDto>(
            async () =>
            {
                var response = await http.GetAsync("api/work-schedules/mine", cancellationToken);
                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    return response;
                }

                withoutSchedule = true;
                response.Dispose();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            },
            cancellationToken);

        return new ApiCallResult<WorkScheduleDto?>(withoutSchedule ? null : result.Value, result.Error);
    }

    public void Dispose() => http.Dispose();
}
