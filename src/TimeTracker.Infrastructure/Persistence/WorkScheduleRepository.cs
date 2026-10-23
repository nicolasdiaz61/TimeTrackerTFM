using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Application.WorkSchedules;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class WorkScheduleRepository(TimeTrackerDbContext context) : IWorkScheduleRepository
{
    public async Task<IReadOnlyList<WorkSchedule>> ListAsync(CancellationToken cancellationToken)
        => await context.WorkSchedules.AsNoTracking().OrderBy(schedule => schedule.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> AssignedCountsAsync(CancellationToken cancellationToken)
        => await context.Users
            .AsNoTracking()
            .Where(user => user.WorkScheduleId != null)
            .GroupBy(user => user.WorkScheduleId!.Value)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken);

    public Task<WorkSchedule?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.WorkSchedules.AsNoTracking().FirstOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);

    public Task<WorkSchedule?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken)
        => context.WorkSchedules.FirstOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken) => context.WorkSchedules.CountAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        var lower = name.ToLowerInvariant();
        return context.WorkSchedules.AnyAsync(schedule => schedule.Id != excludingId && schedule.Name.ToLower() == lower, cancellationToken);
    }

    public async Task AddAsync(WorkSchedule schedule, CancellationToken cancellationToken)
    {
        context.WorkSchedules.Add(schedule);
        await SaveAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => SaveAsync(cancellationToken);

    public async Task RemoveAsync(WorkSchedule schedule, CancellationToken cancellationToken)
    {
        context.WorkSchedules.Remove(schedule);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: UserConfiguration.WorkScheduleForeignKeyName })
        {
            context.ChangeTracker.Clear();
            throw new WorkScheduleInUseException();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: WorkScheduleConfiguration.NameIndexName })
        {
            context.ChangeTracker.Clear();
            throw new WorkScheduleNameTakenException();
        }
    }
}
