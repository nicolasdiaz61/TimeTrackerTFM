using TimeTracker.Application.Identity;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Fakes;

internal sealed class FixedCurrentUser(Guid employeeId, UserRole role = UserRole.Employee, Guid? companyId = null) : ICurrentUser
{
    public Guid EmployeeId { get; } = employeeId;

    public Guid CompanyId { get; } = companyId ?? Company.DefaultId;

    public UserRole Role { get; } = role;
}
