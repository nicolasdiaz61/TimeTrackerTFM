namespace TimeTracker.Api.Employees;

/// <summary>
/// Los cuatro datos del alta y nada más (asignación masiva, API3): las propiedades desconocidas del cuerpo (<c>companyId</c>,
/// <c>passwordHash</c>, <c>temporaryPassword</c>) se ignoran y no tienen efecto. La compañía sale del token y la contraseña temporal
/// la genera el servidor.
/// </summary>
public sealed record CreateEmployeeRequest(string? DisplayName, string? Email, string? Role, Guid? ManagerId);
