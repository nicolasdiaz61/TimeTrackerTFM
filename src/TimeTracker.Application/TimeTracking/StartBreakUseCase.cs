using TimeTracker.Application.Identity;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.TimeTracking;

public sealed class StartBreakUseCase(ITimeEntryRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    /// <summary>Cierra el trabajo abierto y abre una pausa (<c>Break</c> o <c>MealBreak</c>) en una sola operación atómica.</summary>
    public async Task<TimeEntryResult> ExecuteAsync(TimeEntryType breakType, CancellationToken cancellationToken)
    {
        var work = await repository.FindOpenAsync(currentUser.EmployeeId, cancellationToken)
            ?? throw new NotWorkingException();

        var pause = work.Pause(breakType, timeProvider.GetUtcNow());
        await repository.ReplaceOpenAsync(work, pause, cancellationToken);

        return TimeEntryResult.From(pause);
    }
}
