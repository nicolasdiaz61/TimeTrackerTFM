using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Authentication;

public sealed class LoginUseCase(IUserRepository users, IPasswordHasher hasher, SessionIssuer sessions)
{
    // Tope de contraseña: evita hashear cadenas enormes (DoS, API4).
    public const int MaxPasswordLength = 128;

    /// <summary>Devuelve <c>null</c> ante cualquier fallo (forma inválida, correo desconocido o contraseña incorrecta): el mismo 401 genérico.</summary>
    public async Task<LoginResult?> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        if (!HasValidShape(command, out var email, out var password))
        {
            return null;
        }

        var user = await users.FindByEmailAsync(UserAccount.NormalizeEmail(email), cancellationToken);

        // Se verifica siempre, también sin usuario (hash nulo): el tiempo no delata si el correo existe (DD-6).
        var passwordMatches = hasher.Verify(user?.PasswordHash, password);
        if (user is null || !passwordMatches)
        {
            return null;
        }

        // La contraseña se verifica siempre antes: una compañía suspendida tarda lo mismo que unas credenciales erróneas (DD-6).
        return await sessions.IssueAsync(user, cancellationToken);
    }

    // Una entrada inválida no consulta ni hashea nada y no revela nada del usuario (DD-7).
    private static bool HasValidShape(LoginCommand command, out string email, out string password)
    {
        email = command.Email ?? string.Empty;
        password = command.Password ?? string.Empty;

        return !string.IsNullOrWhiteSpace(email)
            && email.Length <= UserAccount.MaxEmailLength
            && !string.IsNullOrWhiteSpace(password)
            && password.Length <= MaxPasswordLength;
    }
}
