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

    /// <summary>Nombre visible del usuario, o <c>null</c> si ya no existe (la auditoría no depende del ciclo de vida del usuario).</summary>
    Task<string?> FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(UserAccount user, CancellationToken cancellationToken);

    /// <summary>
    /// Indica si algún usuario de la compañía del contexto tiene un correo que termina en <paramref name="domain"/> (con la arroba, p. ej.
    /// <c>@example.test</c>). La comparación es literal. Lo usa el validador de arranque de usuarios de demostración (DD-22).
    /// </summary>
    Task<bool> ExistsWithEmailDomainAsync(string domain, CancellationToken cancellationToken);
}
