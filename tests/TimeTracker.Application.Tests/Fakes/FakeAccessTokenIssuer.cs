using TimeTracker.Application.Authentication;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>Emisor falso que registra el usuario y el nombre de compañía recibidos y devuelve un token predecible.</summary>
internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public static readonly DateTimeOffset ExpiresAt = new(2026, 10, 12, 8, 30, 0, TimeSpan.Zero);

    public List<UserAccount> IssuedFor { get; } = [];

    public List<string> CompanyNames { get; } = [];

    /// <summary>Si se fija, la emisión lanza esta excepción.</summary>
    public Exception? Failure { get; set; }

    public AccessToken Issue(UserAccount user, string companyName)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        IssuedFor.Add(user);
        CompanyNames.Add(companyName);
        return new AccessToken($"token-de-{user.Id}", ExpiresAt);
    }
}
