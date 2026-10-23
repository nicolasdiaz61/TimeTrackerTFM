using TimeTracker.Application.WorkSchedules;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>Repositorio en memoria de una sola compañía; los recuentos de asignación salen de los usuarios del repositorio de usuarios.</summary>
internal sealed class InMemoryWorkScheduleRepository(InMemoryUserRepository users) : IWorkScheduleRepository
{
    private readonly List<WorkSchedule> _schedules = [];

    public IReadOnlyList<WorkSchedule> All => _schedules;

    public void Seed(params WorkSchedule[] schedules) => _schedules.AddRange(schedules);

    public Task<IReadOnlyList<WorkSchedule>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkSchedule> list = [.. _schedules.OrderBy(schedule => schedule.Name, StringComparer.Ordinal)];
        return Task.FromResult(list);
    }

    public Task<IReadOnlyDictionary<Guid, int>> AssignedCountsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, int> counts = users.All
            .Where(user => user.WorkScheduleId is not null)
            .GroupBy(user => user.WorkScheduleId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        return Task.FromResult(counts);
    }

    public Task<WorkSchedule?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_schedules.FirstOrDefault(schedule => schedule.Id == id));

    public Task<WorkSchedule?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) => FindByIdAsync(id, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(_schedules.Count);

    public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
        => Task.FromResult(_schedules.Any(schedule => schedule.Id != excludingId && string.Equals(schedule.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(WorkSchedule schedule, CancellationToken cancellationToken)
    {
        _schedules.Add(schedule);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveAsync(WorkSchedule schedule, CancellationToken cancellationToken)
    {
        _schedules.Remove(schedule);
        return Task.CompletedTask;
    }
}
