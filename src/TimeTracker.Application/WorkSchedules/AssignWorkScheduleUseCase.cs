using TimeTracker.Application.Identity;
using TimeTracker.Application.Users;

namespace TimeTracker.Application.WorkSchedules;

/// <summary>
/// Asigna (o retira, con <see langword="null"/>) la jornada de una persona del equipo del Manager. Reutiliza la regla de equipo
/// del resto de la API: solo los reportes directos del Manager.
/// </summary>
public sealed class AssignWorkScheduleUseCase(IUserRepository users, IWorkScheduleRepository schedules, ICurrentUser currentUser)
{
    public async Task<AssignWorkScheduleOutcome> ExecuteAsync(Guid memberId, Guid? workScheduleId, CancellationToken cancellationToken)
    {
        if (await users.FindDirectReportForUpdateAsync(currentUser.EmployeeId, memberId, cancellationToken) is not { } member)
        {
            return AssignWorkScheduleOutcome.NotFound;
        }

        if (workScheduleId is { } id && await schedules.FindByIdAsync(id, cancellationToken) is null)
        {
            return AssignWorkScheduleOutcome.NotFound;
        }

        member.AssignWorkSchedule(workScheduleId);
        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentUserChangeException)
        {
            return AssignWorkScheduleOutcome.Conflict;
        }

        return AssignWorkScheduleOutcome.Assigned;
    }
}
