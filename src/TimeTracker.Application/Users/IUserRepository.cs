using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Users;

public interface IUserRepository
{
    /// <summary>Busca por correo ya normalizado (<see cref="UserAccount.NormalizeEmail"/>); devuelve <c>null</c> si no existe.</summary>
    Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Devuelve como máximo <paramref name="limit"/> reportes directos del responsable, por <c>DisplayName</c> ascendente.</summary>
    Task<IReadOnlyList<UserAccount>> ListDirectReportsAsync(Guid managerId, int limit, CancellationToken cancellationToken);

    /// <summary>Devuelve al miembro solo si es reporte directo del responsable; en otro caso (ajeno, inexistente o el propio responsable) <c>null</c>.</summary>
    Task<UserAccount?> FindDirectReportAsync(Guid managerId, Guid memberId, CancellationToken cancellationToken);

    Task AddAsync(UserAccount user, CancellationToken cancellationToken);
}
