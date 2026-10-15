using TimeTracker.Application.Identity;

namespace TimeTracker.Application.TimeTracking;

public sealed class GetTimeEntriesUseCase(ITimeEntryRepository repository, ICurrentUser currentUser)
{
    // Límite fijo y no controlable por el cliente (evita listados sin cota) cuando no se pide ventana.
    public const int MaxEntries = 300;

    // Tope de seguridad de una ventana: con 32 días como máximo no es la protección principal (ver TimeWindow).
    public const int MaxWindowEntries = 1000;

    /// <summary>Con ventana devuelve los intervalos que la solapan; sin ella, los últimos <see cref="MaxEntries"/>.</summary>
    public async Task<TimeEntryListResult> ExecuteAsync(TimeWindow? window, CancellationToken cancellationToken)
    {
        var employeeId = currentUser.EmployeeId;
        var entries = window is null
            ? await repository.ListLatestAsync(employeeId, MaxEntries, cancellationToken)
            : await repository.ListInWindowAsync(employeeId, window, MaxWindowEntries, cancellationToken);
        var open = await repository.FindOpenAsync(employeeId, cancellationToken);

        return TimeEntryListResult.From(entries, open);
    }
}
