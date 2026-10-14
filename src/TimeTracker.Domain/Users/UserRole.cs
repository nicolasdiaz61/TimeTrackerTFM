namespace TimeTracker.Domain.Users;

/// <summary>Rol de un usuario: un empleado ficha; un responsable, además, consulta a su equipo.</summary>
/// <remarks>Se guarda como texto (<c>Employee</c>/<c>Manager</c>); los valores numéricos no se persisten.</remarks>
public enum UserRole
{
    Employee = 0,
    Manager = 1,
}
