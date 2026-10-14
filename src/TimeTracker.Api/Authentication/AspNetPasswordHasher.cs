using Microsoft.AspNetCore.Identity;

using TimeTracker.Application.Authentication;

namespace TimeTracker.Api.Authentication;

/// <summary>
/// Adaptador de <see cref="IPasswordHasher"/> sobre el <see cref="PasswordHasher{TUser}"/> del marco compartido
/// (PBKDF2 con sal aleatoria). Singleton: calcula una sola vez el hash ficticio con el que iguala el trabajo
/// cuando el usuario no existe (DD-6). Nunca registra contraseñas ni hashes.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly object Subject = new();

    private readonly PasswordHasher<object> _inner = new();
    private readonly string _dummyHash;

    public AspNetPasswordHasher() => _dummyHash = _inner.HashPassword(Subject, Guid.NewGuid().ToString());

    public string Hash(string password) => _inner.HashPassword(Subject, password);

    public bool Verify(string? passwordHash, string password)
    {
        try
        {
            var result = _inner.VerifyHashedPassword(Subject, passwordHash ?? _dummyHash, password);

            return passwordHash is not null && result != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Un hash almacenado que no es Base64 es una credencial no válida, no un fallo del servidor (AU-14).
            return false;
        }
    }
}
