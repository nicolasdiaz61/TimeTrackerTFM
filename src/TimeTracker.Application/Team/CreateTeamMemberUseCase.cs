using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Authentication;
using TimeTracker.Application.Companies;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Team;

/// <summary>
/// Alta de una persona por un Manager (DD-21). La compañía y el responsable por defecto salen de <see cref="ICurrentUser"/>, nunca del
/// cliente; la contraseña temporal la genera el servidor y solo se guarda su hash.
/// </summary>
public sealed class CreateTeamMemberUseCase(
    IUserRepository users,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    ITemporaryPasswordGenerator generator,
    ICompanyLimits limits,
    TimeProvider clock)
{
    // Idéntico para un responsable inexistente, de otra compañía o que no es Manager: no revela cuál de las tres es.
    private static readonly FieldError InvalidManager = new("managerId", "El responsable debe ser un Manager de su compañía.");

    public async Task<CreateTeamMemberResult> ExecuteAsync(CreateTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var errors = Validate(command, out var role);
        if (errors.Count > 0)
        {
            return new CreateTeamMemberResult.Invalid(errors);
        }

        var managerId = command.ManagerId ?? currentUser.EmployeeId;
        if (command.ManagerId is not null && !await IsManagerOfTheCompanyAsync(command.ManagerId.Value, cancellationToken))
        {
            return new CreateTeamMemberResult.Invalid([InvalidManager]);
        }

        var temporaryPassword = generator.Generate();
        var user = UserAccount.Create(
            Guid.CreateVersion7(clock.GetUtcNow()),
            currentUser.CompanyId,
            command.Email!,
            command.DisplayName!,
            hasher.Hash(temporaryPassword),
            role,
            managerId,
            mustChangePassword: true);

        return await users.AddWithinCapAsync(user, limits.MaxEmployees, cancellationToken) switch
        {
            AddUserOutcome.Created => new CreateTeamMemberResult.Created(user, temporaryPassword),
            AddUserOutcome.CapReached => new CreateTeamMemberResult.CapReached(),
            AddUserOutcome.DuplicateEmail => new CreateTeamMemberResult.DuplicateEmail(),
            AddUserOutcome.InvalidManager => new CreateTeamMemberResult.Invalid([InvalidManager]),
            AddUserOutcome.Busy => new CreateTeamMemberResult.Busy(),
            _ => throw new InvalidOperationException("Resultado del alta no contemplado."),
        };
    }

    // El repositorio solo ve la compañía del contexto: un responsable de otra compañía llega aquí como inexistente.
    private async Task<bool> IsManagerOfTheCompanyAsync(Guid managerId, CancellationToken cancellationToken)
        => await users.FindByIdAsync(managerId, cancellationToken) is { Role: UserRole.Manager };

    private static List<FieldError> Validate(CreateTeamMemberCommand command, out UserRole role)
    {
        var errors = new List<FieldError>();
        if (command.DisplayName is null || !UserAccount.IsValidDisplayName(command.DisplayName))
        {
            errors.Add(new FieldError("displayName", $"El nombre debe tener entre 1 y {UserAccount.MaxDisplayNameLength} caracteres."));
        }

        if (command.Email is null || !UserAccount.IsValidEmail(command.Email))
        {
            errors.Add(new FieldError("email", $"El correo electrónico debe ser válido y tener como máximo {UserAccount.MaxEmailLength} caracteres."));
        }

        if (!TryParseRole(command.Role, out role))
        {
            errors.Add(new FieldError("role", "El rol debe ser Employee o Manager."));
        }

        return errors;
    }

    // Solo los dos nombres del catálogo: Enum.TryParse aceptaría también números y valores no definidos.
    private static bool TryParseRole(string? text, out UserRole role)
    {
        role = UserRole.Employee;
        if (text is null)
        {
            return true;
        }

        switch (text.Trim().ToLowerInvariant())
        {
            case "employee":
                return true;
            case "manager":
                role = UserRole.Manager;
                return true;
            default:
                return false;
        }
    }
}
