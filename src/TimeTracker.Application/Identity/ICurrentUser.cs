namespace TimeTracker.Application.Identity;

/// <summary>
/// Puerto de identidad: el empleado autenticado sale siempre de aquí, nunca de un parámetro del cliente.
/// </summary>
public interface ICurrentUser
{
    Guid EmployeeId { get; }
}
