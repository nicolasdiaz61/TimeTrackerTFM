using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>Repositorio de compañías en memoria: devuelve lo sembrado por identificador y cuenta las lecturas.</summary>
internal sealed class InMemoryCompanyRepository : ICompanyRepository
{
    private readonly Dictionary<Guid, CompanyLoginInfo> _companies = [];

    public List<Guid> LoginInfoRequests { get; } = [];

    /// <summary>Si se asigna, la lectura lanza esta excepción (base de datos caída o tabla ausente).</summary>
    public Exception? Failure { get; set; }

    public void Seed(Guid companyId, string name, CompanyStatus status = CompanyStatus.Active, bool usesPlatformAssistant = false)
        => _companies[companyId] = new CompanyLoginInfo(name, status, usesPlatformAssistant);

    public Task EnsureDefaultAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<CompanyLoginInfo?> FindLoginInfoAsync(Guid companyId, CancellationToken cancellationToken)
    {
        LoginInfoRequests.Add(companyId);
        if (Failure is { } failure)
        {
            throw failure;
        }

        return Task.FromResult(_companies.TryGetValue(companyId, out var info) ? info : null);
    }
}
