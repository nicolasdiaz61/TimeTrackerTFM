using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class CompanyRepository(TimeTrackerDbContext context, TimeProvider clock) : ICompanyRepository
{
    public async Task EnsureDefaultAsync(CancellationToken cancellationToken)
    {
        if (await context.Companies.AnyAsync(company => company.Id == Company.DefaultId, cancellationToken))
        {
            return;
        }

        // La clave de plataforma sigue sirviendo a los datos existentes, que cuelgan de esta compañía (DD-3).
        context.Companies.Add(Company.Create(Company.DefaultId, Company.DefaultName, clock.GetUtcNow(), usesPlatformAssistant: true));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CompanyConfiguration.PrimaryKeyName })
        {
            // Otra instancia la insertó entre la comprobación y el guardado: el resultado deseado ya existe. Un conflicto del nombre
            // normalizado con otra compañía no cuadra con la clave primaria y se propaga.
            context.ChangeTracker.Clear();
        }
    }
}
