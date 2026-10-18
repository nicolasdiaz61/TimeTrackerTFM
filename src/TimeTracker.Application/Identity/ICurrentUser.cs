using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Identity;

/// <summary>
/// Puerto de identidad: el empleado y su rol salen siempre de aquí (del token), nunca de un parámetro del cliente.
/// </summary>
public interface ICurrentUser
{
    Guid EmployeeId { get; }

    UserRole Role { get; }
}
