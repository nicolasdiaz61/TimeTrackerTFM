using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Employees;

/// <summary>
/// Persona creada. <see cref="TemporaryPassword"/> es la única vez que existe en claro: no se vuelve a devolver en ninguna otra
/// respuesta. Sin hash ni compañía.
/// </summary>
public sealed record CreateEmployeeResponse(Guid Id, string DisplayName, string Email, string Role, string TemporaryPassword)
{
    public static CreateEmployeeResponse From(UserAccount user, string temporaryPassword)
        => new(user.Id, user.DisplayName, user.Email, user.Role.ToString(), temporaryPassword);

    // El registro de un record imprime todas sus propiedades: nunca la contraseña temporal.
    public override string ToString() => nameof(CreateEmployeeResponse);
}
