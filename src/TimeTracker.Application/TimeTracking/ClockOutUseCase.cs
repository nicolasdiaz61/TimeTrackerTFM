using TimeTracker.Application.Identity;

namespace TimeTracker.Application.TimeTracking;

public sealed class ClockOutUseCase(ITimeEntryRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<TimeEntryResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var entry = await repository.FindOpenAsync(currentUser.EmployeeId, cancellationToken)
            ?? throw new NoOpenTimeEntryException();

        entry.Close(timeProvider.GetUtcNow());
        await repository.UpdateAsync(entry, cancellationToken);

        return TimeEntryResult.From(entry);
    }
}
