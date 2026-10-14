using TimeTracker.Application.Identity;

namespace TimeTracker.Application.TimeTracking;

public sealed class GetTimeEntriesUseCase(ITimeEntryRepository repository, ICurrentUser currentUser)
{
    // Límite fijo y no controlable por el cliente (evita listados sin cota).
    public const int MaxEntries = 50;

    public async Task<TimeEntryListResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var entries = await repository.ListLatestAsync(currentUser.EmployeeId, MaxEntries, cancellationToken);

        return TimeEntryListResult.From(entries);
    }
}
