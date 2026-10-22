namespace TimeTracker.Application.WorkSchedules;

public sealed class ListWorkSchedulesUseCase(IWorkScheduleRepository schedules)
{
    public async Task<IReadOnlyList<WorkScheduleView>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var list = await schedules.ListAsync(cancellationToken);
        var counts = await schedules.AssignedCountsAsync(cancellationToken);

        return [.. list.Select(schedule => new WorkScheduleView(schedule, counts.GetValueOrDefault(schedule.Id)))];
    }
}
