using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Authentication;

public interface IAccessTokenIssuer
{
    /// <summary>Emite el token del usuario; <paramref name="companyName"/> viaja solo como dato de presentación (DD-6).</summary>
    AccessToken Issue(UserAccount user, string companyName);
}
