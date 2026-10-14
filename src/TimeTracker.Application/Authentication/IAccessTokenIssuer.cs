using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Authentication;

public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount user);
}
