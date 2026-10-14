using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class TimeEntryRepository(TimeTrackerDbContext context) : ITimeEntryRepository
{
    // Con seguimiento: UpdateAsync persiste la entidad cargada por FindOpenAsync.
    public Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken)
        => context.TimeEntries.FirstOrDefaultAsync(entry => entry.EmployeeId == employeeId && entry.ClockOutAt == null, cancellationToken);

    public async Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken)
        => await context.TimeEntries
            .AsNoTracking()
            .Where(entry => entry.EmployeeId == employeeId)
            .OrderByDescending(entry => entry.ClockInAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        context.TimeEntries.Add(entry);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TimeEntryConfiguration.OpenEntryIndexName })
        {
            throw new OpenTimeEntryAlreadyExistsException();
        }
    }

    public async Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        // Salida simple, sin control de concurrencia (DD-9 diferida). Update también cubre entidades sin seguimiento.
        context.TimeEntries.Update(entry);
        await context.SaveChangesAsync(cancellationToken);
    }
}
