using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Identity;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Application.WorkSchedules;

/// <summary>Alta de una jornada por un Manager. La compañía sale de <see cref="ICurrentUser"/>, nunca del cliente.</summary>
public sealed class CreateWorkScheduleUseCase(IWorkScheduleRepository schedules, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<SaveWorkScheduleResult> ExecuteAsync(WorkScheduleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var details = command.ToDetails();
        if (WorkSchedule.Validate(details) is { Count: > 0 } errors)
        {
            return new SaveWorkScheduleResult.Invalid([.. errors.Select(error => new FieldError(error.Field, error.Message))]);
        }

        // Tope blando: dos altas simultáneas podrían superarlo por una unidad; basta para frenar el abuso.
        if (await schedules.CountAsync(cancellationToken) >= WorkScheduleLimits.MaxPerCompany)
        {
            return new SaveWorkScheduleResult.CapReached();
        }

        if (await schedules.NameExistsAsync(details.Name!.Trim(), null, cancellationToken))
        {
            return new SaveWorkScheduleResult.DuplicateName();
        }

        var schedule = WorkSchedule.Create(Guid.CreateVersion7(clock.GetUtcNow()), currentUser.CompanyId, details);
        try
        {
            await schedules.AddAsync(schedule, cancellationToken);
        }
        catch (WorkScheduleNameTakenException)
        {
            return new SaveWorkScheduleResult.DuplicateName();
        }

        return new SaveWorkScheduleResult.Saved(new WorkScheduleView(schedule, 0));
    }
}
