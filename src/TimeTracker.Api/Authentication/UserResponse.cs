using TimeTracker.Application.Authentication;

namespace TimeTracker.Api.Authentication;

/// <summary>
/// Usuario autenticado sin su hash. <c>Role</c> viaja como texto (<c>Employee</c> / <c>Manager</c>). Lleva el nombre de la
/// compañía y el estado de la contraseña, pero nunca el identificador de la compañía (DD-6).
/// </summary>
public sealed record UserResponse(Guid Id, string Email, string DisplayName, string Role, string CompanyName, bool MustChangePassword)
{
    public static UserResponse From(UserSummary user)
        => new(user.Id, user.Email, user.DisplayName, user.Role.ToString(), user.CompanyName, user.MustChangePassword);
}
