using TimeTracker.Application.Identity;
using TimeTracker.Application.Users;

namespace TimeTracker.Application.Team;

public sealed class GetTeamMembersUseCase(IUserRepository users, ICurrentUser currentUser)
{
    // Lista acotada (API4, DD-16): el límite lo fija el servidor, no el cliente.
    public const int MaxMembers = 100;

    public async Task<IReadOnlyList<TeamMemberResult>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var reports = await users.ListDirectReportsAsync(currentUser.EmployeeId, MaxMembers, cancellationToken);

        return [.. reports.Select(report => new TeamMemberResult(report.Id, report.DisplayName, report.WorkScheduleId))];
    }
}
