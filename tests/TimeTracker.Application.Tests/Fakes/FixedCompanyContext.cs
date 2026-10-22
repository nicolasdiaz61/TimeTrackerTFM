using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Tests.Fakes;

internal sealed class FixedCompanyContext(Guid? companyId) : ICompanyContext
{
    public Guid? CompanyId { get; } = companyId;
}
