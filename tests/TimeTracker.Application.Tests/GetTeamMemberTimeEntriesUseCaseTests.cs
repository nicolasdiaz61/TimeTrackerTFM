using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests;

public class GetTeamMemberTimeEntriesUseCaseTests
{
    private static readonly Guid MartaId = Guid.Parse("0197a1b2-6c3d-7e4f-8a5b-6c7d8e9f0a60");
    private static readonly Guid LuisId = Guid.Parse("0197a1b2-7c3d-7e4f-8a5b-6c7d8e9f0a71");
    private static readonly Guid AnaId = Guid.Parse("0197a1b2-8c3d-7e4f-8a5b-6c7d8e9f0a82");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryTimeEntryRepository _entries = new();

    private GetTeamMemberTimeEntriesUseCase CreateUseCase() => new(_users, _entries, new FixedCurrentUser(MartaId));

    private static UserAccount NewUser(Guid id, string name, UserRole role, Guid? managerId)
        => UserAccount.Create(id, $"{Guid.NewGuid():N}@example.test", name, "hash-de-prueba", role, managerId);

    private TimeEntry SeedClosed(Guid employeeId, DateTimeOffset clockIn, int minutes)
    {
        var entry = TimeEntry.Start(employeeId, TimeEntryType.Work, clockIn);
        entry.Close(clockIn.AddMinutes(minutes));
        _entries.Seed(entry);
        return entry;
    }

    [Fact]
    public async Task Get_ForADirectReport_ReturnsEntriesAndTotal()
    {
        _users.Seed(NewUser(LuisId, "Luis Empleado", UserRole.Employee, MartaId));
        SeedClosed(LuisId, EightAm, 60);
        SeedClosed(LuisId, EightAm.AddHours(2), 30);
        var open = TimeEntry.Start(LuisId, TimeEntryType.Work, EightAm.AddHours(4));
        _entries.Seed(open);

        var result = await CreateUseCase().ExecuteAsync(LuisId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(90, result.TotalWorkedMinutes);
        Assert.Equal(3, result.Items.Count);
        Assert.Null(result.Items.Single(item => item.Id == open.Id).DurationMinutes);
    }

    [Fact]
    public async Task Get_ForADirectReport_IncludesBreaksTotalsAndStatus()
    {
        _users.Seed(NewUser(LuisId, "Luis Empleado", UserRole.Employee, MartaId));
        SeedClosed(LuisId, EightAm, 60);
        var pause = TimeEntry.Start(LuisId, TimeEntryType.Break, EightAm.AddHours(1));
        pause.Close(EightAm.AddHours(1).AddMinutes(15));
        _entries.Seed(pause);
        _entries.Seed(TimeEntry.Start(LuisId, TimeEntryType.MealBreak, EightAm.AddHours(2)));

        var result = await CreateUseCase().ExecuteAsync(LuisId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(60, result.TotalWorkedMinutes);
        Assert.Equal(15, result.TotalBreakMinutes);
        Assert.Equal(0, result.TotalMealBreakMinutes);
        Assert.Equal(TimeTrackingStatus.OnMealBreak, result.Status);
        Assert.Equal([TimeEntryType.MealBreak, TimeEntryType.Break, TimeEntryType.Work], result.Items.Select(item => item.Type));
    }

    private static readonly Guid OtherManagerId = Guid.Parse("0197a1b2-9c3d-7e4f-8a5b-6c7d8e9f0a93");
    private static readonly Guid ForeignMemberId = Guid.Parse("0197a1b2-ac3d-7e4f-8a5b-6c7d8e9f0aa4");
    private static readonly Guid UnmanagedId = Guid.Parse("0197a1b2-bc3d-7e4f-8a5b-6c7d8e9f0ab5");

    public static TheoryData<Guid> NonDirectReports =>
    [
        ForeignMemberId,
        UnmanagedId,
        MartaId,
        Guid.Parse("0197a1b2-cc3d-7e4f-8a5b-6c7d8e9f0ac6"),
    ];

    [Theory]
    [MemberData(nameof(NonDirectReports))]
    public async Task Get_ForNonDirectReport_ReturnsNullWithoutQueryingEntries(Guid memberId)
    {
        _users.Seed(
            NewUser(MartaId, "Marta Responsable", UserRole.Manager, null),
            NewUser(LuisId, "Luis Empleado", UserRole.Employee, MartaId),
            NewUser(OtherManagerId, "Carlos Responsable", UserRole.Manager, null),
            NewUser(ForeignMemberId, "Eva Ajena", UserRole.Employee, OtherManagerId),
            NewUser(UnmanagedId, "Pablo Empleado", UserRole.Employee, null));
        SeedClosed(ForeignMemberId, EightAm, 60);
        SeedClosed(UnmanagedId, EightAm, 60);
        SeedClosed(MartaId, EightAm, 60);

        var result = await CreateUseCase().ExecuteAsync(memberId, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _entries.ListLatestCalls);
    }

    [Fact]
    public async Task Get_WithMoreThanFiftyEntries_ReturnsFiftyMostRecentAndTotal500()
    {
        _users.Seed(NewUser(LuisId, "Luis Empleado", UserRole.Employee, MartaId));
        for (var day = 0; day < 55; day++)
        {
            SeedClosed(LuisId, EightAm.AddDays(day), 10);
        }

        var result = await CreateUseCase().ExecuteAsync(LuisId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(500, result.TotalWorkedMinutes);
        Assert.Equal(EightAm.AddDays(54), result.Items[0].ClockInAt);
    }

    [Fact]
    public async Task Get_ForMemberWithoutEntries_ReturnsEmptyAndZero()
    {
        _users.Seed(NewUser(LuisId, "Luis Empleado", UserRole.Employee, MartaId));

        var result = await CreateUseCase().ExecuteAsync(LuisId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalWorkedMinutes);
    }

    [Fact]
    public async Task Get_OnlyQueriesTheMembersEntries()
    {
        _users.Seed(
            NewUser(LuisId, "Luis Empleado", UserRole.Employee, MartaId),
            NewUser(AnaId, "Ana Empleada", UserRole.Employee, MartaId));
        var luisEntry = SeedClosed(LuisId, EightAm, 60);
        SeedClosed(AnaId, EightAm, 45);
        SeedClosed(MartaId, EightAm, 30);

        var result = await CreateUseCase().ExecuteAsync(LuisId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal([luisEntry.Id], result.Items.Select(item => item.Id));
        Assert.Equal(60, result.TotalWorkedMinutes);
    }
}
