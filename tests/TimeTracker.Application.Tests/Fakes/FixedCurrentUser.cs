using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Tests.Fakes;

internal sealed class FixedCurrentUser(Guid employeeId) : ICurrentUser
{
    public Guid EmployeeId { get; } = employeeId;
}
