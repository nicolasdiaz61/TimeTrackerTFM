using TimeTracker.Application.Identity;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.TimeTracking;

public sealed class ClockInUseCase(ITimeEntryRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<TimeEntryResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        // Solo da el mensaje claro en el caso normal; la garantía ante carreras es el índice único de la base de datos.
        if (await repository.FindOpenAsync(currentUser.EmployeeId, cancellationToken) is not null)
        {
            throw new OpenTimeEntryAlreadyExistsException();
        }

        var entry = TimeEntry.Start(currentUser.EmployeeId, TimeEntryType.Work, timeProvider.GetUtcNow());
        await repository.AddAsync(entry, cancellationToken);
        return TimeEntryResult.From(entry);
    }
}
