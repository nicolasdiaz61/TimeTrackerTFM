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

    public Task AddAsync(UserAccount user, CancellationToken cancellationToken)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }
}
