using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>
/// Almacén de registro en memoria: devuelve el resultado programado y, solo si crea, deja la compañía visible para el emisor de
/// sesiones (como lo estaría tras confirmar la transacción). Puede fallar al insertar para comprobar que no se emite nada.
/// </summary>
internal sealed class FakeCompanyRegistrationStore(InMemoryCompanyRepository companies) : ICompanyRegistrationStore
{
    public RegistrationOutcome Outcome { get; set; } = RegistrationOutcome.Created;

    public Exception? Failure { get; set; }

    public List<(Company Company, UserAccount Manager)> Calls { get; } = [];

    public Task<RegistrationOutcome> CreateAsync(Company company, UserAccount manager, CancellationToken cancellationToken)
    {
        Calls.Add((company, manager));
        if (Failure is { } failure)
        {
            throw failure;
        }

        if (Outcome == RegistrationOutcome.Created)
        {
            companies.Seed(company.Id, company.Name, company.Status, company.UsesPlatformAssistant);
        }

        return Task.FromResult(Outcome);
    }
}
