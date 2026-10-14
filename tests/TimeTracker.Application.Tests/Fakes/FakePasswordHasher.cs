using TimeTracker.Application.Authentication;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>Hasher falso y determinista que cuenta las verificaciones y las hechas con hash nulo.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int VerifyCalls { get; private set; }

    public int NullHashVerifications { get; private set; }

    public string? LastVerifiedPassword { get; private set; }

    public string Hash(string password) => HashOf(password);

    public bool Verify(string? passwordHash, string password)
    {
        VerifyCalls++;
        LastVerifiedPassword = password;
        if (passwordHash is null)
        {
            NullHashVerifications++;
            return false;
        }

        return passwordHash == HashOf(password);
    }

    public static string HashOf(string password) => $"hash:{password}";
}
