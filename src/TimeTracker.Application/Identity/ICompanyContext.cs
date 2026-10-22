namespace TimeTracker.Application.Identity;

/// <summary>
/// Puerto de la compañía en curso. Es nulo cuando no hay ninguna (nunca una compañía por defecto): el filtro de
/// consulta que lo use falla cerrado y no devuelve filas. A diferencia de <see cref="ICurrentUser"/>, no lanza.
/// </summary>
public interface ICompanyContext
{
    Guid? CompanyId { get; }
}
