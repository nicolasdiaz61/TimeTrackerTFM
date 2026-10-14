using Microsoft.EntityFrameworkCore;

using TimeTracker.Application.DemoData;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class DemoTimeEntryStore(TimeTrackerDbContext context) : IDemoTimeEntryStore
{
    public async Task<IReadOnlyList<DateTimeOffset>> ListStartsBetweenAsync(
        Guid employeeId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
        => await context.TimeEntries
            .AsNoTracking()
            .Where(entry => entry.EmployeeId == employeeId && entry.ClockInAt >= fromInclusive && entry.ClockInAt < toExclusive)
            .Select(entry => entry.ClockInAt)
            .ToListAsync(cancellationToken);

    // Un único SaveChanges: EF lo ejecuta en una transacción, así que se guarda todo o nada.
    public async Task AddRangeAsync(IReadOnlyCollection<TimeEntry> entries, CancellationToken cancellationToken)
    {
        context.TimeEntries.AddRange(entries);
        await context.SaveChangesAsync(cancellationToken);
    }
}
