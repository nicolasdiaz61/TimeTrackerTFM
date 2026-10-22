using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Application.WorkSchedules;

public sealed class UpdateWorkScheduleUseCase(IWorkScheduleRepository schedules)
{
    public async Task<SaveWorkScheduleResult> ExecuteAsync(Guid id, WorkScheduleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // El 404 va primero: una jornada ajena es igual que una inexistente, también con un cuerpo inválido.
        if (await schedules.FindForUpdateAsync(id, cancellationToken) is not { } schedule)
        {
            return new SaveWorkScheduleResult.NotFound();
        }

        var details = command.ToDetails();
        if (WorkSchedule.Validate(details) is { Count: > 0 } errors)
        {
            return new SaveWorkScheduleResult.Invalid([.. errors.Select(error => new FieldError(error.Field, error.Message))]);
        }

        if (await schedules.NameExistsAsync(details.Name!.Trim(), id, cancellationToken))
        {
            return new SaveWorkScheduleResult.DuplicateName();
        }

        schedule.Update(details);
        try
        {
            await schedules.SaveChangesAsync(cancellationToken);
        }
        catch (WorkScheduleNameTakenException)
        {
            return new SaveWorkScheduleResult.DuplicateName();
        }

        var counts = await schedules.AssignedCountsAsync(cancellationToken);
        return new SaveWorkScheduleResult.Saved(new WorkScheduleView(schedule, counts.GetValueOrDefault(id)));
    }
}
