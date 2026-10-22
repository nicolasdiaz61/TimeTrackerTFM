using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Authentication;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Companies;

/// <summary>
/// Autorregistro de una compañía y su primer Manager (DD-19). Orden fijo: código de invitación, campos, hash y almacén; así solo quien
/// conoce un código válido puede observar un conflicto de correo o de nombre. No fija la compañía de la escritura (lo hace el almacén)
/// y no depende de ningún puerto de correo: no hay verificación de correo.
/// </summary>
public sealed class RegisterCompanyUseCase(
    IInviteCodeValidator inviteCodes,
    IPasswordHasher hasher,
    ICompanyRegistrationStore store,
    SessionIssuer sessions,
    TimeProvider clock)
{
    public async Task<RegisterCompanyResult> ExecuteAsync(RegisterCompanyCommand command, CancellationToken cancellationToken)
    {
        if (!inviteCodes.IsValid(command.InviteCode))
        {
            return new RegisterCompanyResult.InvalidInviteCode();
        }

        var errors = Validate(command);
        if (errors.Count > 0)
        {
            return new RegisterCompanyResult.Invalid(errors);
        }

        var now = clock.GetUtcNow();
        var company = Company.Create(Guid.CreateVersion7(now), command.CompanyName!, now);
        var manager = UserAccount.Create(
            Guid.CreateVersion7(now),
            company.Id,
            command.Email!,
            command.ManagerName!,
            hasher.Hash(command.Password!),
            UserRole.Manager,
            managerId: null);

        var outcome = await store.CreateAsync(company, manager, cancellationToken);
        return outcome switch
        {
            RegistrationOutcome.Created => await IssueSessionAsync(manager, company, cancellationToken),
            RegistrationOutcome.DuplicateName => new RegisterCompanyResult.DuplicateName(),
            RegistrationOutcome.DuplicateEmail => new RegisterCompanyResult.DuplicateEmail(),
            _ => throw new InvalidOperationException("Resultado del registro no contemplado."),
        };
    }

    private async Task<RegisterCompanyResult> IssueSessionAsync(UserAccount manager, Company created, CancellationToken cancellationToken)
    {
        // La compañía nace Active: si el emisor común la rechaza algo es incoherente y no se entrega ninguna sesión.
        var session = await sessions.IssueAsync(manager, cancellationToken)
            ?? throw new InvalidOperationException("La compañía recién creada no puede iniciar sesión.");
        return new RegisterCompanyResult.Created(session, created.Id);
    }

    private static List<FieldError> Validate(RegisterCompanyCommand command)
    {
        var errors = new List<FieldError>();
        if (command.CompanyName is null || !Company.IsValidName(command.CompanyName))
        {
            errors.Add(new FieldError("companyName", $"El nombre de la compañía debe tener entre 1 y {Company.MaxNameLength} caracteres."));
        }

        if (command.ManagerName is null || !UserAccount.IsValidDisplayName(command.ManagerName))
        {
            errors.Add(new FieldError("managerName", $"El nombre de la persona debe tener entre 1 y {UserAccount.MaxDisplayNameLength} caracteres."));
        }

        if (command.Email is null || !UserAccount.IsValidEmail(command.Email))
        {
            errors.Add(new FieldError("email", $"El correo electrónico debe ser válido y tener como máximo {UserAccount.MaxEmailLength} caracteres."));
        }

        if (PasswordPolicy.Validate(command.Password, "password") is { } passwordError)
        {
            errors.Add(passwordError);
        }

        return errors;
    }
}
