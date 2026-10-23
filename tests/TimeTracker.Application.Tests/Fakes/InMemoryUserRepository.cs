using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>
/// Repositorio en memoria que respeta el contrato del puerto: la búsqueda por correo es por igualdad exacta
/// (no normaliza, para obligar al caso de uso a hacerlo), los reportes se filtran por <c>ManagerId</c>,
/// se ordenan por nombre y respetan el límite, y solo los reportes directos se encuentran como tales.
/// </summary>
internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<UserAccount> _users = [];

    public IReadOnlyList<UserAccount> All => _users;

    public int FindByEmailCalls { get; private set; }

    public string? LastEmailRequested { get; private set; }

    public int? LastLimit { get; private set; }

    public void Seed(params UserAccount[] users) => _users.AddRange(users);

    public Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        FindByEmailCalls++;
        LastEmailRequested = normalizedEmail;
        return Task.FromResult(_users.FirstOrDefault(user => user.Email == normalizedEmail));
    }

    public Task<IReadOnlyList<UserAccount>> ListDirectReportsAsync(Guid managerId, int limit, CancellationToken cancellationToken)
    {
        LastLimit = limit;
        IReadOnlyList<UserAccount> reports = [.. _users
            .Where(user => user.ManagerId == managerId)
            .OrderBy(user => user.DisplayName, StringComparer.Ordinal)
            .Take(limit)];
        return Task.FromResult(reports);
    }

    public Task<UserAccount?> FindDirectReportAsync(Guid managerId, Guid memberId, CancellationToken cancellationToken)
        => Task.FromResult(_users.FirstOrDefault(user => user.Id == memberId && user.ManagerId == managerId));

    public Task<string?> FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_users.FirstOrDefault(user => user.Id == userId)?.DisplayName);

    public Task<bool> ExistsWithEmailDomainAsync(string domain, CancellationToken cancellationToken)
        => Task.FromResult(_users.Any(user => user.Email.EndsWith(domain, StringComparison.Ordinal)));

    public Task AddAsync(UserAccount user, CancellationToken cancellationToken)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public List<(UserAccount User, int MaxUsers)> AddWithinCapCalls { get; } = [];

    /// <summary>Si se asigna, <see cref="AddWithinCapAsync"/> devuelve este resultado sin añadir nada.</summary>
    public AddUserOutcome? ForcedOutcome { get; set; }

    public int SaveChangesCalls { get; private set; }

    /// <summary>Si se asigna, <see cref="SaveChangesAsync"/> lanza esta excepción (conflicto de concurrencia de la fila).</summary>
    public Exception? SaveFailure { get; set; }

    public Task<UserAccount?> FindForUpdateAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_users.FirstOrDefault(user => user.Id == userId));

    public Task<UserAccount?> FindDirectReportForUpdateAsync(Guid managerId, Guid memberId, CancellationToken cancellationToken)
        => FindDirectReportAsync(managerId, memberId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return SaveFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }

    public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_users.FirstOrDefault(user => user.Id == userId));

    public Task<AddUserOutcome> AddWithinCapAsync(UserAccount user, int maxUsers, CancellationToken cancellationToken)
    {
        AddWithinCapCalls.Add((user, maxUsers));
        if (ForcedOutcome is { } forced)
        {
            return Task.FromResult(forced);
        }

        if (_users.Any(existing => existing.Email == user.Email))
        {
            return Task.FromResult(AddUserOutcome.DuplicateEmail);
        }

        if (_users.Count(existing => existing.CompanyId == user.CompanyId) >= maxUsers)
        {
            return Task.FromResult(AddUserOutcome.CapReached);
        }

        _users.Add(user);
        return Task.FromResult(AddUserOutcome.Created);
    }
}
