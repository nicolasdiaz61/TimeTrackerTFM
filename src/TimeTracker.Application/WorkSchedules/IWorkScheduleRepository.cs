using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Application.WorkSchedules;

/// <summary>Jornadas de la compañía del contexto: ninguna operación recibe una compañía y las de otra compañía no existen para ellas.</summary>
public interface IWorkScheduleRepository
{
    Task<IReadOnlyList<WorkSchedule>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Personas con cada jornada asignada (solo las jornadas que tienen alguna).</summary>
    Task<IReadOnlyDictionary<Guid, int>> AssignedCountsAsync(CancellationToken cancellationToken);

    Task<WorkSchedule?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Igual que <see cref="FindByIdAsync"/>, pero seguida para guardar cambios con <see cref="SaveChangesAsync"/>.</summary>
    Task<WorkSchedule?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>Indica si hay otra jornada con ese nombre (sin distinguir mayúsculas), ignorando <paramref name="excludingId"/>.</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken);

    /// <exception cref="WorkScheduleNameTakenException">Nombre repetido.</exception>
    Task AddAsync(WorkSchedule schedule, CancellationToken cancellationToken);

    /// <exception cref="WorkScheduleNameTakenException">Nombre repetido.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <exception cref="WorkScheduleInUseException">Hay personas con la jornada asignada.</exception>
    Task RemoveAsync(WorkSchedule schedule, CancellationToken cancellationToken);
}
