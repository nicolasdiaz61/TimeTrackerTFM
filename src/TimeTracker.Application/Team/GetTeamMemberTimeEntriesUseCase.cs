using TimeTracker.Application.Identity;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Application.Users;

namespace TimeTracker.Application.Team;

public sealed class GetTeamMemberTimeEntriesUseCase(IUserRepository users, ITimeEntryRepository entries, ICurrentUser currentUser)
{
    /// <summary>
    /// Devuelve los fichajes de un reporte directo del usuario actual; para cualquier otro (ajeno, inexistente
    /// o el propio responsable) devuelve <c>null</c> sin consultar fichajes, de modo que el 404 sea idéntico (DD-15).
    /// </summary>
    public async Task<TimeEntryListResult?> ExecuteAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await users.FindDirectReportAsync(currentUser.EmployeeId, memberId, cancellationToken);
        if (member is null)
        {
            return null;
        }

        var latest = await entries.ListLatestAsync(member.Id, GetTimeEntriesUseCase.MaxEntries, cancellationToken);

        return TimeEntryListResult.From(latest);
    }
}
