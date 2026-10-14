using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.DemoData;

/// <summary>Puerto de la siembra de fichajes de demostración: consulta de inicios existentes e inserción atómica.</summary>
public interface IDemoTimeEntryStore
{
    /// <summary>Devuelve los <c>ClockInAt</c> de los intervalos del empleado en <c>[fromInclusive, toExclusive)</c>.</summary>
    Task<IReadOnlyList<DateTimeOffset>> ListStartsBetweenAsync(Guid employeeId, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken);

    /// <summary>Persiste todos los intervalos en una única transacción: o se guardan todos o ninguno.</summary>
    Task AddRangeAsync(IReadOnlyCollection<TimeEntry> entries, CancellationToken cancellationToken);
}
