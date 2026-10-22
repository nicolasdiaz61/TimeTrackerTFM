using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Identity;

/// <summary>
/// Puerto de identidad: el empleado, su compañía y su rol salen siempre de aquí (del token), nunca de un parámetro del cliente.
/// </summary>
public interface ICurrentUser
{
    Guid EmployeeId { get; }

    /// <summary>
    /// Compañía del usuario autenticado. Lanza una excepción si no hay usuario autenticado o su compañía no es válida: nunca
    /// devuelve una compañía por defecto. (A diferencia de <see cref="ICompanyContext"/>, que es nulo sin compañía.)
    /// </summary>
    Guid CompanyId { get; }

    UserRole Role { get; }
}
