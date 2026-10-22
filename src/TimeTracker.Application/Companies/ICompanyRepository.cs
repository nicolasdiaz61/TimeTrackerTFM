namespace TimeTracker.Application.Companies;

public interface ICompanyRepository
{
    /// <summary>
    /// Crea la compañía por defecto (compañía de demostración, con el identificador fijo) si no existe. Es idempotente
    /// y segura ante arranques concurrentes: si otra instancia la inserta antes, se da por existente.
    /// </summary>
    Task EnsureDefaultAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Nombre, estado e indicador de asistente de la compañía de un usuario ya identificado, o <c>null</c> si no existe. Es una
    /// lectura por clave primaria de <c>companies</c>, que no lleva filtro de compañía (DD-8).
    /// </summary>
    Task<CompanyLoginInfo?> FindLoginInfoAsync(Guid companyId, CancellationToken cancellationToken);
}
