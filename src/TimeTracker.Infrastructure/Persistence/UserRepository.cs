using Microsoft.EntityFrameworkCore;

using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class UserRepository(TimeTrackerDbContext context) : IUserRepository
{
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
}
