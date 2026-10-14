using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.TimeTracking;

public interface ITimeEntryRepository
{
    Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken);

    /// <summary>Devuelve como máximo <paramref name="limit"/> fichajes del empleado, por <c>ClockInAt</c> descendente.</summary>
    Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken);

    /// <summary>Persiste un fichaje nuevo; si ya hay otro abierto del empleado lanza <c>OpenTimeEntryAlreadyExistsException</c>.</summary>
    Task AddAsync(TimeEntry entry, CancellationToken cancellationToken);

    /// <summary>Persiste los cambios de un fichaje existente (salida simple, sin control de concurrencia: DD-9 diferida).</summary>
    Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken);
}
