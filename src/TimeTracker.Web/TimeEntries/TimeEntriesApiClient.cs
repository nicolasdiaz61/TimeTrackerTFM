using TimeTracker.Web.Http;

namespace TimeTracker.Web.TimeEntries;

/// <summary>Cliente tipado de /api/time-entries. La base de la URL viene del HttpClient (ApiBaseUrl).</summary>
public sealed class TimeEntriesApiClient(HttpClient http) : IDisposable
{
    public Task<TimeEntryListDto> GetAsync(CancellationToken cancellationToken = default)
        => ApiRequest.SendAsync<TimeEntryListDto>(() => http.GetAsync("api/time-entries", cancellationToken), cancellationToken);

    public Task<TimeEntryDto> ClockInAsync(CancellationToken cancellationToken = default)
        => ApiRequest.SendAsync<TimeEntryDto>(() => http.PostAsync("api/time-entries/clock-in", content: null, cancellationToken), cancellationToken);

    public Task<TimeEntryDto> ClockOutAsync(CancellationToken cancellationToken = default)
        => ApiRequest.SendAsync<TimeEntryDto>(() => http.PostAsync("api/time-entries/clock-out", content: null, cancellationToken), cancellationToken);

    public void Dispose() => http.Dispose();
}
