using TimeTracker.Application.Companies;

namespace TimeTracker.Application.Tests.Fakes;

internal sealed class FixedCompanyLimits(int maxEmployees) : ICompanyLimits
{
    public int MaxEmployees { get; } = maxEmployees;
}
