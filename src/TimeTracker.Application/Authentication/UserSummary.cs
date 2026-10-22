using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Authentication;

/// <summary>Datos públicos del usuario: nunca incluye el hash de la contraseña ni el identificador de su compañía.</summary>
public sealed record UserSummary(Guid Id, string Email, string DisplayName, UserRole Role, string CompanyName, bool MustChangePassword)
{
    public static UserSummary From(UserAccount user, string companyName)
        => new(user.Id, user.Email, user.DisplayName, user.Role, companyName, user.MustChangePassword);
}
