using TimeTracker.Application.Identity;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.Security;

/// <summary>
/// Adaptador de <see cref="ICompanyContext"/> (solo en Api, DD-7): una instancia por ámbito, de escritura única. Sin compañía
/// devuelve <c>null</c> y nunca una compañía por defecto; el filtro de consulta que lo use falla cerrado.
/// </summary>
public sealed class AmbientCompanyContext : ICompanyContext, ICompanyContextSetter
{
    public Guid? CompanyId { get; private set; }

    public void Set(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("La compañía del contexto no puede estar vacía.", nameof(companyId));
        }

        if (CompanyId is { } current && current != companyId)
        {
            // Sin identificadores en el mensaje (A09).
            throw new InvalidOperationException("El contexto de compañía ya está fijado a otra compañía.");
        }

        CompanyId = companyId;
    }
}
