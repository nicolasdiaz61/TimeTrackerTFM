using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class TimeEntryRepository(TimeTrackerDbContext context) : ITimeEntryRepository
{
    // Con seguimiento: UpdateAsync y ReplaceOpenAsync persisten la entidad cargada por FindOpenAsync.
    public Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken)
        => context.TimeEntries.FirstOrDefaultAsync(entry => entry.EmployeeId == employeeId && entry.ClockOutAt == null, cancellationToken);

    public async Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken)
        => await context.TimeEntries
            .AsNoTracking()
            .Where(entry => entry.EmployeeId == employeeId)
            .OrderByDescending(entry => entry.ClockInAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TimeEntry>> ListInWindowAsync(Guid employeeId, TimeWindow window, int limit, CancellationToken cancellationToken)
        => await context.TimeEntries
            .AsNoTracking()
            .Where(entry => entry.EmployeeId == employeeId
                && entry.ClockInAt < window.To
                && (entry.ClockOutAt == null || entry.ClockOutAt > window.From))
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
        catch (DbUpdateException exception) when (IsOpenEntryViolation(exception))
        {
            throw new OpenTimeEntryAlreadyExistsException();
        }
    }

    public async Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        // Update también cubre entidades sin seguimiento; xmin detecta que otra operación cambió la fila.
        context.TimeEntries.Update(entry);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrentTimeEntryChangeException();
        }
    }

    public async Task ReplaceOpenAsync(TimeEntry closed, TimeEntry next, CancellationToken cancellationToken)
    {
        // El cierre va primero y en la misma transacción: el índice único parcial no es diferible, así que
        // insertar el nuevo abierto antes de cerrar el anterior lo violaría.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            context.TimeEntries.Update(closed);
            await context.SaveChangesAsync(cancellationToken);

            context.TimeEntries.Add(next);
            await context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException || IsOpenEntryViolation(exception))
        {
            // Al salir sin commit, Dispose revierte la transacción; el contexto se limpia por si se reutiliza.
            context.ChangeTracker.Clear();
            throw new ConcurrentTimeEntryChangeException();
        }
    }

    private static bool IsOpenEntryViolation(Exception exception)
        => exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TimeEntryConfiguration.OpenEntryIndexName } };
}
