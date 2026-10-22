using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Users;

namespace TimeTracker.Application.Authentication;

/// <summary>
/// Cambio de contraseña de la persona del token (DD-21). Comprueba antes de cambiar nada que su compañía está activa, verifica la
/// contraseña actual con el hasher, exige que la nueva cumpla <see cref="PasswordPolicy"/> y sea distinta, y guarda bajo el token
/// <c>xmin</c> de la fila. La sesión nueva sale del emisor común.
/// </summary>
public sealed class ChangePasswordUseCase(IUserRepository users, ICurrentUser currentUser, IPasswordHasher hasher, SessionIssuer sessions)
{
    private const string CurrentField = "currentPassword";

    private const string NewField = "newPassword";

    public async Task<ChangePasswordResult> ExecuteAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindForUpdateAsync(currentUser.EmployeeId, cancellationToken);
        if (user is null || !await sessions.IsCompanyActiveAsync(user.CompanyId, cancellationToken))
        {
            return new ChangePasswordResult.Unauthorized();
        }

        var errors = Validate(command, user.PasswordHash);
        if (errors.Count > 0)
        {
            return new ChangePasswordResult.Invalid(errors);
        }

        user.ChangePassword(hasher.Hash(command.NewPassword!));
        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentUserChangeException)
        {
            return new ChangePasswordResult.Conflict();
        }

        // Pasa por el emisor común también aquí: si la compañía dejó de estar activa entre medias, no se entrega ninguna sesión.
        return await sessions.IssueAsync(user, cancellationToken) is { } session
            ? new ChangePasswordResult.Changed(session)
            : new ChangePasswordResult.Unauthorized();
    }

    private List<FieldError> Validate(ChangePasswordCommand command, string currentHash)
    {
        var errors = new List<FieldError>();
        var current = command.CurrentPassword;
        var currentIsKnown = false;

        // Una contraseña vacía o enorme no se hashea (DoS, API4) ni se compara con nada.
        if (string.IsNullOrEmpty(current) || current.Length > LoginUseCase.MaxPasswordLength)
        {
            errors.Add(new FieldError(CurrentField, "La contraseña actual no es correcta."));
        }
        else if (!hasher.Verify(currentHash, current))
        {
            errors.Add(new FieldError(CurrentField, "La contraseña actual no es correcta."));
        }
        else
        {
            currentIsKnown = true;
        }

        if (PasswordPolicy.Validate(command.NewPassword, NewField) is { } policyError)
        {
            errors.Add(policyError);
        }
        else if (currentIsKnown && string.Equals(command.NewPassword, current, StringComparison.Ordinal))
        {
            errors.Add(new FieldError(NewField, "La nueva contraseña debe ser distinta de la actual."));
        }

        return errors;
    }
}
