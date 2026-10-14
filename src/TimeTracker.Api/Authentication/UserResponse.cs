using TimeTracker.Application.Authentication;

namespace TimeTracker.Api.Authentication;

/// <summary>Usuario autenticado sin su hash. <c>Role</c> viaja como texto (<c>Employee</c> / <c>Manager</c>).</summary>
public sealed record UserResponse(Guid Id, string Email, string DisplayName, string Role)
{
    public static UserResponse From(UserSummary user) => new(user.Id, user.Email, user.DisplayName, user.Role.ToString());
}
