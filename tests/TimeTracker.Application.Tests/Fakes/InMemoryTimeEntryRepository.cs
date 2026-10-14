using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>
/// Repositorio en memoria que respeta el contrato del puerto (filtra por empleado, orden descendente,
/// límite) y registra lo que los casos de uso añaden, actualizan y reemplazan.
/// </summary>
internal sealed class InMemoryTimeEntryRepository : ITimeEntryRepository
{
    private readonly List<TimeEntry> _entries = [];

    public List<TimeEntry> Added { get; } = [];

    public List<TimeEntry> Updated { get; } = [];

    public List<(TimeEntry Closed, TimeEntry Next)> Replaced { get; } = [];

    public IReadOnlyList<TimeEntry> Entries => _entries;

    public int? LastRequestedLimit { get; private set; }

    public int ListLatestCalls { get; private set; }

    /// <summary>Carga un fichaje previo sin contarlo como añadido por el caso de uso.</summary>
    public void Seed(TimeEntry entry) => _entries.Add(entry);

    public Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken)
        => Task.FromResult(_entries.FirstOrDefault(entry => entry.EmployeeId == employeeId && entry.IsOpen));

    public Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken)
    {
        ListLatestCalls++;
        LastRequestedLimit = limit;
        IReadOnlyList<TimeEntry> latest = [.. _entries
            .Where(entry => entry.EmployeeId == employeeId)
            .OrderByDescending(entry => entry.ClockInAt)
            .Take(limit)];
        return Task.FromResult(latest);
    }

    public Task AddAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        Added.Add(entry);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        Updated.Add(entry);
        return Task.CompletedTask;
    }

    public Task ReplaceOpenAsync(TimeEntry closed, TimeEntry next, CancellationToken cancellationToken)
    {
        _entries.Add(next);
        Replaced.Add((closed, next));
        return Task.CompletedTask;
    }
}
