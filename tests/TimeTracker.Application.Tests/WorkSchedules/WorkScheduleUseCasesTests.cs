using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.Users;
using TimeTracker.Application.WorkSchedules;
using TimeTracker.Domain.Users;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Application.Tests.WorkSchedules;

public class WorkScheduleUseCasesTests
{
    private static readonly Guid CompanyId = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");
    private static readonly Guid ManagerId = Guid.Parse("0197a1b2-6c3d-7e4f-8a5b-6c7d8e9f0a60");

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryWorkScheduleRepository _schedules;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero));

    public WorkScheduleUseCasesTests() => _schedules = new InMemoryWorkScheduleRepository(_users);

    private FixedCurrentUser Manager => new(ManagerId, UserRole.Manager, CompanyId);

    private static WorkScheduleCommand Command(string? name = "Oficina", IReadOnlyCollection<DayOfWeek>? days = null)
        => new(name, new TimeOnly(9, 0), new TimeOnly(14, 0), 60, 30, days ?? [DayOfWeek.Monday, DayOfWeek.Friday]);

    private static WorkSchedule Schedule(string name = "Existente")
        => WorkSchedule.Create(Guid.NewGuid(), CompanyId, new(name, new TimeOnly(8, 0), new TimeOnly(13, 0), 45, 15, [DayOfWeek.Monday]));

    private static UserAccount Employee(Guid? managerId = null)
    {
        var id = Guid.NewGuid();
        return UserAccount.Create(id, CompanyId, $"{id:N}@example.test", "Persona", "hash", UserRole.Employee, managerId ?? ManagerId);
    }

    private CreateWorkScheduleUseCase Create() => new(_schedules, Manager, _clock);

    private UpdateWorkScheduleUseCase Update() => new(_schedules);

    private AssignWorkScheduleUseCase Assign() => new(_users, _schedules, Manager);

    [Fact]
    public async Task Create_WithValidData_StoresTheScheduleInTheCurrentCompany()
    {
        var result = await Create().ExecuteAsync(Command(" Oficina "), CancellationToken.None);

        var saved = Assert.IsType<SaveWorkScheduleResult.Saved>(result);
        var stored = Assert.Single(_schedules.All);
        Assert.Equal(CompanyId, stored.CompanyId);
        Assert.Equal("Oficina", stored.Name);
        Assert.Equal(0, saved.View.AssignedCount);
        Assert.NotEqual(Guid.Empty, stored.Id);
    }

    [Fact]
    public async Task Create_WithInvalidData_ReturnsFieldErrorsAndStoresNothing()
    {
        var result = await Create().ExecuteAsync(Command(name: " ", days: []), CancellationToken.None);

        var invalid = Assert.IsType<SaveWorkScheduleResult.Invalid>(result);
        Assert.Contains(invalid.Errors, error => error.Field == "name");
        Assert.Contains(invalid.Errors, error => error.Field == "days");
        Assert.Empty(_schedules.All);
    }

    [Fact]
    public async Task Create_WithTheSameNameInTheCompany_IgnoringCase_IsADuplicate()
    {
        _schedules.Seed(Schedule("Oficina"));

        var result = await Create().ExecuteAsync(Command("  oficina"), CancellationToken.None);

        Assert.IsType<SaveWorkScheduleResult.DuplicateName>(result);
        Assert.Single(_schedules.All);
    }

    [Fact]
    public async Task Create_AtTheCap_IsRejected()
    {
        _schedules.Seed([.. Enumerable.Range(0, WorkScheduleLimits.MaxPerCompany).Select(index => Schedule($"Jornada {index}"))]);

        var result = await Create().ExecuteAsync(Command("Una más"), CancellationToken.None);

        Assert.IsType<SaveWorkScheduleResult.CapReached>(result);
        Assert.Equal(WorkScheduleLimits.MaxPerCompany, _schedules.All.Count);
    }

    [Fact]
    public async Task Update_ReplacesTheDetails_AndReportsTheAssignedCount()
    {
        var schedule = Schedule("Antigua");
        _schedules.Seed(schedule);
        var employee = Employee();
        employee.AssignWorkSchedule(schedule.Id);
        _users.Seed(employee);

        var result = await Update().ExecuteAsync(schedule.Id, Command("Nueva"), CancellationToken.None);

        var saved = Assert.IsType<SaveWorkScheduleResult.Saved>(result);
        Assert.Equal("Nueva", schedule.Name);
        Assert.Equal(1, saved.View.AssignedCount);
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_IsNotADuplicate()
    {
        var schedule = Schedule("Oficina");
        _schedules.Seed(schedule);

        Assert.IsType<SaveWorkScheduleResult.Saved>(await Update().ExecuteAsync(schedule.Id, Command("Oficina"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_WithTheNameOfAnotherSchedule_IsADuplicate()
    {
        var other = Schedule("Otra");
        var schedule = Schedule("Oficina");
        _schedules.Seed(other, schedule);

        Assert.IsType<SaveWorkScheduleResult.DuplicateName>(await Update().ExecuteAsync(schedule.Id, Command("Otra"), CancellationToken.None));
        Assert.Equal("Oficina", schedule.Name);
    }

    [Fact]
    public async Task Update_OfAnUnknownSchedule_IsNotFound()
        => Assert.IsType<SaveWorkScheduleResult.NotFound>(await Update().ExecuteAsync(Guid.NewGuid(), Command(), CancellationToken.None));

    [Fact]
    public async Task Update_WithInvalidData_ReturnsFieldErrors()
    {
        var schedule = Schedule();
        _schedules.Seed(schedule);

        Assert.IsType<SaveWorkScheduleResult.Invalid>(await Update().ExecuteAsync(schedule.Id, Command(name: ""), CancellationToken.None));
        Assert.Equal("Existente", schedule.Name);
    }

    [Fact]
    public async Task List_ReturnsTheSchedulesByNameWithTheirAssignedCount()
    {
        var first = Schedule("A");
        var second = Schedule("B");
        _schedules.Seed(second, first);
        var employee = Employee();
        employee.AssignWorkSchedule(second.Id);
        _users.Seed(employee);

        var views = await new ListWorkSchedulesUseCase(_schedules).ExecuteAsync(CancellationToken.None);

        Assert.Equal(["A", "B"], views.Select(view => view.Schedule.Name));
        Assert.Equal([0, 1], views.Select(view => view.AssignedCount));
    }

    [Fact]
    public async Task Delete_RemovesAnUnassignedSchedule()
    {
        var schedule = Schedule();
        _schedules.Seed(schedule);

        Assert.Equal(DeleteWorkScheduleOutcome.Deleted, await new DeleteWorkScheduleUseCase(_schedules).ExecuteAsync(schedule.Id, CancellationToken.None));
        Assert.Empty(_schedules.All);
    }

    [Fact]
    public async Task Delete_OfAnAssignedSchedule_IsInUseAndKeepsIt()
    {
        var schedule = Schedule();
        _schedules.Seed(schedule);
        var employee = Employee();
        employee.AssignWorkSchedule(schedule.Id);
        _users.Seed(employee);

        Assert.Equal(DeleteWorkScheduleOutcome.InUse, await new DeleteWorkScheduleUseCase(_schedules).ExecuteAsync(schedule.Id, CancellationToken.None));
        Assert.Single(_schedules.All);
    }

    [Fact]
    public async Task Delete_OfAnUnknownSchedule_IsNotFound()
        => Assert.Equal(DeleteWorkScheduleOutcome.NotFound, await new DeleteWorkScheduleUseCase(_schedules).ExecuteAsync(Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task Assign_SetsTheScheduleOfADirectReport_AndUnassignClearsIt()
    {
        var schedule = Schedule();
        _schedules.Seed(schedule);
        var employee = Employee();
        _users.Seed(employee);

        Assert.Equal(AssignWorkScheduleOutcome.Assigned, await Assign().ExecuteAsync(employee.Id, schedule.Id, CancellationToken.None));
        Assert.Equal(schedule.Id, employee.WorkScheduleId);
        Assert.Equal(1, _users.SaveChangesCalls);

        Assert.Equal(AssignWorkScheduleOutcome.Assigned, await Assign().ExecuteAsync(employee.Id, null, CancellationToken.None));
        Assert.Null(employee.WorkScheduleId);
    }

    [Fact]
    public async Task Assign_ToSomeoneWhoIsNotADirectReport_IsNotFound()
    {
        var schedule = Schedule();
        _schedules.Seed(schedule);
        var stranger = Employee(managerId: Guid.NewGuid());
        _users.Seed(stranger);

        Assert.Equal(AssignWorkScheduleOutcome.NotFound, await Assign().ExecuteAsync(stranger.Id, schedule.Id, CancellationToken.None));
        Assert.Null(stranger.WorkScheduleId);
        Assert.Equal(AssignWorkScheduleOutcome.NotFound, await Assign().ExecuteAsync(Guid.NewGuid(), schedule.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Assign_OfAnUnknownSchedule_IsNotFound_AndChangesNothing()
    {
        var employee = Employee();
        _users.Seed(employee);

        Assert.Equal(AssignWorkScheduleOutcome.NotFound, await Assign().ExecuteAsync(employee.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.Null(employee.WorkScheduleId);
    }

    [Fact]
    public async Task Assign_WhenTheRowChangedConcurrently_IsAConflict()
    {
        var schedule = Schedule();
        _schedules.Seed(schedule);
        var employee = Employee();
        _users.Seed(employee);
        _users.SaveFailure = new ConcurrentUserChangeException();

        Assert.Equal(AssignWorkScheduleOutcome.Conflict, await Assign().ExecuteAsync(employee.Id, schedule.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Mine_ReturnsTheAssignedScheduleOrNull()
    {
        var schedule = Schedule();
        _schedules.Seed(schedule);
        var employee = Employee();
        var without = Employee();
        _users.Seed(employee, without);
        employee.AssignWorkSchedule(schedule.Id);

        Assert.Same(schedule, await new GetMyWorkScheduleUseCase(_users, _schedules, new FixedCurrentUser(employee.Id, UserRole.Employee, CompanyId)).ExecuteAsync(CancellationToken.None));
        Assert.Null(await new GetMyWorkScheduleUseCase(_users, _schedules, new FixedCurrentUser(without.Id, UserRole.Employee, CompanyId)).ExecuteAsync(CancellationToken.None));
    }
}
