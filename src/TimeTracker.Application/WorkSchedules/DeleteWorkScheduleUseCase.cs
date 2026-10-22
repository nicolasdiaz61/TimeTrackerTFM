namespace TimeTracker.Application.WorkSchedules;

public sealed class DeleteWorkScheduleUseCase(IWorkScheduleRepository schedules)
{
    public async Task<DeleteWorkScheduleOutcome> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await schedules.FindForUpdateAsync(id, cancellationToken) is not { } schedule)
        {
            return DeleteWorkScheduleOutcome.NotFound;
        }

        // Sin borrado en cascada: una jornada con personas asignadas no se borra.
        var counts = await schedules.AssignedCountsAsync(cancellationToken);
        if (counts.GetValueOrDefault(id) > 0)
        {
            return DeleteWorkScheduleOutcome.InUse;
        }

        try
        {
            await schedules.RemoveAsync(schedule, cancellationToken);
        }
        catch (WorkScheduleInUseException)
        {
            return DeleteWorkScheduleOutcome.InUse;
        }

        return DeleteWorkScheduleOutcome.Deleted;
    }
}
