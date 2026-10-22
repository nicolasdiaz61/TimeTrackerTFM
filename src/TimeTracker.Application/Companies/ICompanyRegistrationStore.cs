using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Companies;

/// <summary>
/// Puerto de escritura del autorregistro. Crea la compañía y su primer Manager de forma atómica: o quedan las dos filas o ninguna.
/// Quién fija la compañía de la escritura es la implementación (Infrastructure); Application no la conoce (DD-19, DD-7).
/// </summary>
public interface ICompanyRegistrationStore
{
    Task<RegistrationOutcome> CreateAsync(Company company, UserAccount manager, CancellationToken cancellationToken);
}
