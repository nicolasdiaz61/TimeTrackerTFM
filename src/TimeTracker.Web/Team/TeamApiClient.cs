using TimeTracker.Web.Http;
using TimeTracker.Web.TimeEntries;

namespace TimeTracker.Web.Team;

/// <summary>Cliente tipado de /api/team (solo lectura). Exige el rol Manager en el servidor.</summary>
public sealed class TeamApiClient(HttpClient http) : IDisposable
{
    public Task<TeamMemberListDto> GetMembersAsync(CancellationToken cancellationToken = default)
        => ApiRequest.SendAsync<TeamMemberListDto>(() => http.GetAsync("api/team/members", cancellationToken), cancellationToken);

    public Task<TimeEntryListDto> GetTimeEntriesAsync(Guid memberId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => ApiRequest.SendAsync<TimeEntryListDto>(
            () => http.GetAsync(new Uri($"api/team/members/{memberId:D}/time-entries?{TimeWindowQuery.Build(from, to)}", UriKind.Relative), cancellationToken),
            cancellationToken);

    public void Dispose() => http.Dispose();
}
