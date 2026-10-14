using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Authentication;

/// <summary>Datos públicos del usuario: nunca incluye el hash de la contraseña.</summary>
public sealed record UserSummary(Guid Id, string Email, string DisplayName, UserRole Role)
{
    public static UserSummary From(UserAccount user) => new(user.Id, user.Email, user.DisplayName, user.Role);
}
