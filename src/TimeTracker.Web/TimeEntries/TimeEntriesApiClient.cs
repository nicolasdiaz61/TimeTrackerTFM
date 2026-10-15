using TimeTracker.Web.Formatting;
using TimeTracker.Web.Http;

namespace TimeTracker.Web.TimeEntries;

/// <summary>Cliente tipado de /api/time-entries. La base de la URL viene del HttpClient (ApiBaseUrl).</summary>
public sealed class TimeEntriesApiClient(HttpClient http) : IDisposable
{
    /// <summary>Intervalos que solapan la ventana <c>[from, to)</c> (UTC) y el estado actual de la persona.</summary>
    public Task<TimeEntryListDto> GetAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => ApiRequest.SendAsync<TimeEntryListDto>(
            () => http.GetAsync(new Uri($"api/time-entries?{TimeWindowQuery.Build(from, to)}", UriKind.Relative), cancellationToken),
            cancellationToken);

    public Task<TimeEntryDto> ClockInAsync(CancellationToken cancellationToken = default)
        => PostAsync("clock-in", cancellationToken);

    public Task<TimeEntryDto> ClockOutAsync(CancellationToken cancellationToken = default)
        => PostAsync("clock-out", cancellationToken);

    public Task<TimeEntryDto> StartBreakAsync(CancellationToken cancellationToken = default)
        => PostAsync("start-break", cancellationToken);

    public Task<TimeEntryDto> StartMealBreakAsync(CancellationToken cancellationToken = default)
        => PostAsync("start-meal-break", cancellationToken);

    public Task<TimeEntryDto> EndBreakAsync(CancellationToken cancellationToken = default)
        => PostAsync("end-break", cancellationToken);

    /// <summary>Ejecuta la acción elegida en la pantalla contra su endpoint.</summary>
    public Task<TimeEntryDto> RunAsync(ClockAction action, CancellationToken cancellationToken = default) => action switch
    {
        ClockAction.ClockIn => ClockInAsync(cancellationToken),
        ClockAction.StartBreak => StartBreakAsync(cancellationToken),
        ClockAction.StartMealBreak => StartMealBreakAsync(cancellationToken),
        ClockAction.EndBreak => EndBreakAsync(cancellationToken),
        _ => ClockOutAsync(cancellationToken),
    };

    public void Dispose() => http.Dispose();

    private Task<TimeEntryDto> PostAsync(string action, CancellationToken cancellationToken)
        => ApiRequest.SendAsync<TimeEntryDto>(() => http.PostAsync($"api/time-entries/{action}", content: null, cancellationToken), cancellationToken);
}
