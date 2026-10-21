using TimeTracker.Application.Authentication;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>Emisor falso que registra el usuario y el nombre de compañía recibidos y devuelve un token predecible.</summary>
internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public static readonly DateTimeOffset ExpiresAt = new(2026, 10, 12, 8, 30, 0, TimeSpan.Zero);

    public List<UserAccount> IssuedFor { get; } = [];

    public List<string> CompanyNames { get; } = [];

    public AccessToken Issue(UserAccount user, string companyName)
    {
        IssuedFor.Add(user);
        CompanyNames.Add(companyName);
        return new AccessToken($"token-de-{user.Id}", ExpiresAt);
    }
}
