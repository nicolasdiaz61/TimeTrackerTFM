using TimeTracker.Application.Authentication;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>Emisor falso que registra el usuario recibido y devuelve un token predecible.</summary>
internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public static readonly DateTimeOffset ExpiresAt = new(2026, 10, 12, 8, 30, 0, TimeSpan.Zero);

    public List<UserAccount> IssuedFor { get; } = [];

    public AccessToken Issue(UserAccount user)
    {
        IssuedFor.Add(user);
        return new AccessToken($"token-de-{user.Id}", ExpiresAt);
    }
}
