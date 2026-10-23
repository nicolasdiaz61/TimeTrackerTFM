using TimeTracker.Application.Identity;
using TimeTracker.Application.Users;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Application.WorkSchedules;

/// <summary>La jornada de la persona autenticada, o <see langword="null"/> si no tiene ninguna.</summary>
public sealed class GetMyWorkScheduleUseCase(IUserRepository users, IWorkScheduleRepository schedules, ICurrentUser currentUser)
{
    public async Task<WorkSchedule?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(currentUser.EmployeeId, cancellationToken);

        return user?.WorkScheduleId is { } id ? await schedules.FindByIdAsync(id, cancellationToken) : null;
    }
}
