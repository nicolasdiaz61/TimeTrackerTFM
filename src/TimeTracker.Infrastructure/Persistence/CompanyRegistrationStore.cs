using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>
/// Autorregistro (DD-19): crea la compañía y su primer Manager con un único guardado, dentro de un ámbito de la compañía nueva. La
/// petición que registra es anónima y no tiene compañía; abrir el ámbito con el identificador recién generado hace que la guarda de
/// escritura (DD-7) acepte las filas sin ninguna ruta exenta. La unicidad la deciden las restricciones, sin consulta previa.
/// </summary>
public sealed class CompanyRegistrationStore(ICompanyScopeFactory scopes) : ICompanyRegistrationStore
{
    public async Task<RegistrationOutcome> CreateAsync(Company company, UserAccount manager, CancellationToken cancellationToken)
    {
        if (manager.CompanyId != company.Id)
        {
            throw new ArgumentException("El Manager debe pertenecer a la compañía que se crea.", nameof(manager));
        }

        await using var scope = scopes.CreateScope(company.Id);
        var context = scope.ServiceProvider.GetRequiredService<TimeTrackerDbContext>();
        context.Companies.Add(company);
        context.Users.Add(manager);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return RegistrationOutcome.Created;
        }
        catch (DbUpdateException exception) when (ViolatedUniqueConstraint(exception) == CompanyConfiguration.NormalizedNameIndexName)
        {
            return RegistrationOutcome.DuplicateName;
        }
        catch (DbUpdateException exception) when (ViolatedUniqueConstraint(exception) == UserConfiguration.EmailIndexName)
        {
            return RegistrationOutcome.DuplicateEmail;
        }
    }

    // Solo las dos restricciones de unicidad del registro son un conflicto esperable; cualquier otro fallo se propaga.
    private static string? ViolatedUniqueConstraint(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation ? violation.ConstraintName : null;
}
