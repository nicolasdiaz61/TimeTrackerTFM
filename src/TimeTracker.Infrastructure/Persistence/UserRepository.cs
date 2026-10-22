using System.Data;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class UserRepository(TimeTrackerDbContext context) : IUserRepository
{
    // Máximo de reintentos ante un conflicto de serialización del alta con tope (employee-management, «Tope de personas por compañía»).
    internal const int MaxSerializationRetries = 3;

    private const int RetryDelayMilliseconds = 15;

    // Normaliza otra vez (idempotente): la garantía de coincidencia no depende de quien llama.
    public Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var email = UserAccount.NormalizeEmail(normalizedEmail);
        // company-filter-bypass: el login no tiene compañía todavía y el correo es único global (DD-9, sitio 1 de la lista cerrada).
        return context.Users.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public async Task<IReadOnlyList<UserAccount>> ListDirectReportsAsync(Guid managerId, int limit, CancellationToken cancellationToken)
        => await context.Users
            .AsNoTracking()
            .Where(user => user.ManagerId == managerId)
            .OrderBy(user => user.DisplayName)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<UserAccount?> FindDirectReportAsync(Guid managerId, Guid memberId, CancellationToken cancellationToken)
        => context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == memberId && user.ManagerId == managerId, cancellationToken);

    public Task<string?> FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
        => context.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => user.DisplayName).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsWithEmailDomainAsync(string domain, CancellationToken cancellationToken)
        => context.Users.AsNoTracking().AnyAsync(user => user.Email.EndsWith(domain), cancellationToken);

    public async Task AddAsync(UserAccount user, CancellationToken cancellationToken)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
        => context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<UserAccount?> FindForUpdateAsync(Guid userId, CancellationToken cancellationToken)
        => context.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<UserAccount?> FindDirectReportForUpdateAsync(Guid managerId, Guid memberId, CancellationToken cancellationToken)
        => context.Users.FirstOrDefaultAsync(user => user.Id == memberId && user.ManagerId == managerId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // La fila cambió desde su lectura (xmin): no se pisa el cambio ajeno.
            throw new ConcurrentUserChangeException();
        }
    }

    // Recuento (con el filtro de compañía) e inserción en una transacción Serializable (V-14): válida entre instancias y sin SQL
    // crudo. Un conflicto de serialización (40001) se reintenta como máximo MaxSerializationRetries veces; agotados, no se crea nada.
    public async Task<AddUserOutcome> AddWithinCapAsync(UserAccount user, int maxUsers, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await TryAddWithinCapAsync(user, maxUsers, cancellationToken);
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                context.ChangeTracker.Clear();
                if (attempt == MaxSerializationRetries)
                {
                    return AddUserOutcome.Busy;
                }

                // Espera escalonada: reintentar a la vez volvería a colisionar con las demás altas.
                await Task.Delay(RetryDelayMilliseconds * (attempt + 1), cancellationToken);
            }
        }
    }

    private async Task<AddUserOutcome> TryAddWithinCapAsync(UserAccount user, int maxUsers, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await context.Users.CountAsync(cancellationToken) >= maxUsers)
        {
            return AddUserOutcome.CapReached;
        }

        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AddUserOutcome.Created;
        }
        catch (DbUpdateException exception) when (ViolatedConstraint(exception, PostgresErrorCodes.UniqueViolation) == UserConfiguration.EmailIndexName)
        {
            context.ChangeTracker.Clear();
            return AddUserOutcome.DuplicateEmail;
        }
        catch (DbUpdateException exception) when (ViolatedConstraint(exception, PostgresErrorCodes.ForeignKeyViolation) == UserConfiguration.ManagerForeignKeyName)
        {
            context.ChangeTracker.Clear();
            return AddUserOutcome.InvalidManager;
        }
    }

    // PostgreSQL señala el fallo de serialización en la sentencia o en la confirmación, y EF lo envuelve (DbUpdateException al guardar,
    // InvalidOperationException «error al usar una transacción» al confirmar): se recorre toda la cadena de excepciones internas.
    private static bool IsSerializationFailure(Exception exception)
        => exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
            || (exception.InnerException is { } inner && IsSerializationFailure(inner));

    private static string? ViolatedConstraint(DbUpdateException exception, string sqlState)
        => exception.InnerException is PostgresException violation && violation.SqlState == sqlState ? violation.ConstraintName : null;
}
