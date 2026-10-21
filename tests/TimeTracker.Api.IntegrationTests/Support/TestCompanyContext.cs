using TimeTracker.Application.Identity;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>Contexto de compañía de prueba: se puede cambiar entre consultas para comprobar que el filtro se evalúa en cada ejecución.</summary>
internal sealed class TestCompanyContext(Guid? companyId) : ICompanyContext
{
    public Guid? CompanyId { get; set; } = companyId;
}
