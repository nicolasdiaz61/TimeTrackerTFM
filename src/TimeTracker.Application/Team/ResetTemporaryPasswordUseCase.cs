using TimeTracker.Application.Authentication;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Users;

namespace TimeTracker.Application.Team;

/// <summary>
/// Regenera la contraseña temporal de una persona (DD-21). Solo la regenera su responsable directo (mínimo privilegio) y solo mientras
/// la persona no haya cambiado su contraseña; el guardado respeta el token <c>xmin</c> de la fila, de modo que nunca pisa la
/// contraseña que la persona eligió entre tanto.
/// </summary>
public sealed class ResetTemporaryPasswordUseCase(
    IUserRepository users,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    ITemporaryPasswordGenerator generator)
{
    public async Task<ResetTemporaryPasswordResult> ExecuteAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await users.FindDirectReportForUpdateAsync(currentUser.EmployeeId, memberId, cancellationToken);
        if (member is null)
        {
            return new ResetTemporaryPasswordResult.NotFound();
        }

        if (!member.MustChangePassword)
        {
            return new ResetTemporaryPasswordResult.Conflict();
        }

        var temporaryPassword = generator.Generate();
        member.RegenerateTemporaryPassword(hasher.Hash(temporaryPassword));
        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentUserChangeException)
        {
            return new ResetTemporaryPasswordResult.Conflict();
        }

        return new ResetTemporaryPasswordResult.Regenerated(temporaryPassword);
    }
}
