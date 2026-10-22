using TimeTracker.Application.Identity;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>Contexto de compañía escribible para pruebas de Infrastructure (el real vive en Api y no se referencia desde aquí).</summary>
internal sealed class RecordingCompanyContext : ICompanyContext, ICompanyContextSetter
{
    public Guid? CompanyId { get; private set; }

    public void Set(Guid companyId) => CompanyId = companyId;
}

/// <summary>Decora la fábrica real y anota las compañías pedidas.</summary>
internal sealed class RecordingScopeFactory(ICompanyScopeFactory inner) : ICompanyScopeFactory
{
    public List<Guid> Requested { get; } = [];

    public Microsoft.Extensions.DependencyInjection.AsyncServiceScope CreateScope(Guid companyId)
    {
        Requested.Add(companyId);
        return inner.CreateScope(companyId);
    }
}
