using TimeTracker.Application.Identity;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>
/// Contexto de compañía transitorio de la PR 1 (DD-7): una sola compañía, la de por defecto. La PR 2 lo sustituye por
/// el contexto que toma la compañía del token.
/// </summary>
public sealed class FixedCompanyContext : ICompanyContext
{
    public Guid? CompanyId => Company.DefaultId;
}
