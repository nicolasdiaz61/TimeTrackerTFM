using TimeTracker.Application.Identity;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.TimeTracking;

public sealed class EndBreakUseCase(ITimeEntryRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    /// <summary>Cierra la pausa abierta (de cualquier tipo) y reanuda el trabajo en una sola operación atómica.</summary>
    public async Task<TimeEntryResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var pause = await repository.FindOpenAsync(currentUser.EmployeeId, cancellationToken)
            ?? throw new NoOpenBreakException();

        var work = pause.Resume(timeProvider.GetUtcNow());
        await repository.ReplaceOpenAsync(pause, work, cancellationToken);

        return TimeEntryResult.From(work);
    }
}
