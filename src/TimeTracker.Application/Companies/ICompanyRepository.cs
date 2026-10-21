namespace TimeTracker.Application.Companies;

public interface ICompanyRepository
{
    /// <summary>
    /// Crea la compañía por defecto (compañía de demostración, con el identificador fijo) si no existe. Es idempotente
    /// y segura ante arranques concurrentes: si otra instancia la inserta antes, se da por existente.
    /// </summary>
    Task EnsureDefaultAsync(CancellationToken cancellationToken);
}
