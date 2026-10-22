using TimeTracker.Domain.Users;

namespace TimeTracker.Domain.Tests.WorkSchedules;

public class UserWorkScheduleAssignmentTests
{
    private static UserAccount NewUser() => UserAccount.Create(
        Guid.NewGuid(), Guid.NewGuid(), "luis@example.test", "Luis", "hash", UserRole.Employee, Guid.NewGuid());

    [Fact]
    public void ANewUser_HasNoWorkSchedule() => Assert.Null(NewUser().WorkScheduleId);

    [Fact]
    public void AssignWorkSchedule_SetsAndClearsTheSchedule()
    {
        var user = NewUser();
        var scheduleId = Guid.NewGuid();

        user.AssignWorkSchedule(scheduleId);
        Assert.Equal(scheduleId, user.WorkScheduleId);

        user.AssignWorkSchedule(null);
        Assert.Null(user.WorkScheduleId);
    }

    [Fact]
    public void AssignWorkSchedule_WithAnEmptyIdentifier_Throws()
        => Assert.Throws<ArgumentException>(() => NewUser().AssignWorkSchedule(Guid.Empty));
}
