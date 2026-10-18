using TimeTracker.Application.Identity;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Fakes;

internal sealed class FixedCurrentUser(Guid employeeId, UserRole role = UserRole.Employee) : ICurrentUser
{
    public Guid EmployeeId { get; } = employeeId;

    public UserRole Role { get; } = role;
}
