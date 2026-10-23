using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.Time;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Team;

public class GetTeamSummaryUseCaseTests
{
    private static readonly Guid MartaId = Guid.Parse("0197a1b2-6c3d-7e4f-8a5b-6c7d8e9f0a60");
    private static readonly Guid LuisId = Guid.Parse("0197a1b2-7c3d-7e4f-8a5b-6c7d8e9f0a71");
    private static readonly Guid AnaId = Guid.Parse("0197a1b2-8c3d-7e4f-8a5b-6c7d8e9f0a82");
    private static readonly Guid OtherManagerId = Guid.Parse("0197a1b2-9c3d-7e4f-8a5b-6c7d8e9f0a93");
    private static readonly Guid ForeignId = Guid.Parse("0197a1b2-ac3d-7e4f-8a5b-6c7d8e9f0aa4");
    private static readonly DateOnly Day = new(2026, 10, 15);
    private static readonly DateTimeOffset Now = SpainTime.ToUtc(new DateOnly(2026, 10, 18), new TimeOnly(12, 0));

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryTimeEntryRepository _entries = new();

    private static LocalDateRange Week()
    {
        Assert.True(LocalDateRange.TryCreate(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 18), out var range, out _));
        return range!;
    }

    private Task<TeamSummaryResult> RunAsync()
        => new GetTeamSummaryUseCase(_users, _entries, new FixedCurrentUser(MartaId, UserRole.Manager))
            .ExecuteAsync(Week(), Now, CancellationToken.None);

    private void SeedUser(Guid id, string name, Guid? managerId)
        => _users.Seed(UserAccount.Create(id, Company.DefaultId, $"{id:N}@example.test", name, "hash", UserRole.Employee, managerId));

    private void Seed(Guid employee, TimeEntryType type, DateOnly day, TimeOnly from, TimeOnly to)
    {
        var entry = TimeEntry.Start(Company.DefaultId, employee, type, SpainTime.ToUtc(day, from));
        entry.Close(SpainTime.ToUtc(day, to));
        _entries.Seed(entry);
    }

    private void SeedOpen(Guid employee, TimeEntryType type, DateOnly day, TimeOnly from)
        => _entries.Seed(TimeEntry.Start(Company.DefaultId, employee, type, SpainTime.ToUtc(day, from)));

    [Fact]
    public async Task Summary_AggregatesMinutesAndCountsPerMember()
    {
        SeedUser(LuisId, "Luis", MartaId);
        Seed(LuisId, TimeEntryType.Work, Day, new(8, 0), new(12, 0));
        Seed(LuisId, TimeEntryType.Break, Day, new(12, 0), new(12, 10));
        Seed(LuisId, TimeEntryType.Break, Day, new(12, 10), new(12, 15));
        Seed(LuisId, TimeEntryType.MealBreak, Day, new(12, 15), new(13, 5));
        Seed(LuisId, TimeEntryType.Work, Day.AddDays(1), new(8, 0), new(9, 0));

        var result = await RunAsync();

        var luis = Assert.Single(result.Members);
        Assert.Equal(300, luis.WorkedMinutes);
        Assert.Equal(15, luis.BreakMinutes);
        Assert.Equal(2, luis.BreakCount);
        Assert.Equal(50, luis.MealBreakMinutes);
        Assert.Equal(1, luis.MealBreakCount);
        Assert.Equal(2, luis.DaysWithEntries);
        Assert.Equal("5 h 0 min", luis.WorkedText);
        Assert.Equal("0 h 15 min", luis.BreakText);
        Assert.Equal("0 h 50 min", luis.MealBreakText);
        Assert.Equal(new DateOnly(2026, 10, 12), result.From);
        Assert.Equal(new DateOnly(2026, 10, 18), result.To);
    }

    [Fact]
    public async Task Summary_ManyShortBreaksAreDistinguishedFromFewLongOnes()
    {
        SeedUser(LuisId, "Luis", MartaId);
        SeedUser(AnaId, "Ana", MartaId);
        for (var i = 0; i < 4; i++)
        {
            Seed(LuisId, TimeEntryType.Break, Day, new(9 + i, 0), new(9 + i, 5));
        }

        Seed(AnaId, TimeEntryType.Break, Day, new(9, 0), new(10, 0));

        var result = await RunAsync();

        var luis = result.Members.Single(member => member.MemberId == LuisId);
        var ana = result.Members.Single(member => member.MemberId == AnaId);
        Assert.Equal((20, 4), (luis.BreakMinutes, luis.BreakCount));
        Assert.Equal((60, 1), (ana.BreakMinutes, ana.BreakCount));
        Assert.False(luis.AboveAverageBreakMinutes);
        Assert.True(luis.AboveAverageBreakCount);
        Assert.True(ana.AboveAverageBreakMinutes);
        Assert.False(ana.AboveAverageBreakCount);
    }

    [Fact]
    public async Task Summary_ComputesAveragesAndDifferencesAndOrdersByBreakMinutes()
    {
        SeedUser(LuisId, "Luis", MartaId);
        SeedUser(AnaId, "Ana", MartaId);
        Seed(LuisId, TimeEntryType.Work, Day, new(8, 0), new(12, 0));
        Seed(LuisId, TimeEntryType.Break, Day, new(12, 0), new(12, 45));
        Seed(AnaId, TimeEntryType.Work, Day, new(8, 0), new(10, 0));
        Seed(AnaId, TimeEntryType.Break, Day, new(10, 0), new(10, 15));
        Seed(AnaId, TimeEntryType.Break, Day, new(11, 0), new(11, 6));

        var result = await RunAsync();

        Assert.Equal(2, result.MemberCount);
        Assert.False(result.MembersTruncated);
        Assert.Equal(180, result.Averages.WorkedMinutes);
        Assert.Equal(33, result.Averages.BreakMinutes);
        Assert.Equal(1.5, result.Averages.BreakCount);
        Assert.Equal("0 h 33 min", result.Averages.BreakText);
        Assert.Equal([LuisId, AnaId], result.Members.Select(member => member.MemberId));
        Assert.Equal(12, result.Members[0].BreakMinutesVsAverage);
        Assert.Equal(-0.5, result.Members[0].BreakCountVsAverage);
        Assert.Equal(-12, result.Members[1].BreakMinutesVsAverage);
        Assert.Equal(0.5, result.Members[1].BreakCountVsAverage);
        Assert.True(result.Members[0].AboveAverageBreakMinutes);
        Assert.False(result.Members[1].AboveAverageBreakMinutes);
    }

    [Fact]
    public async Task Summary_MembersWithEqualBreaksAreNotAboveAverage()
    {
        SeedUser(LuisId, "Luis", MartaId);
        SeedUser(AnaId, "Ana", MartaId);
        Seed(LuisId, TimeEntryType.Break, Day, new(9, 0), new(9, 10));
        Seed(AnaId, TimeEntryType.Break, Day, new(9, 0), new(9, 10));

        var result = await RunAsync();

        Assert.All(result.Members, member =>
        {
            Assert.False(member.AboveAverageBreakMinutes);
            Assert.False(member.AboveAverageBreakCount);
            Assert.Equal(0, member.BreakMinutesVsAverage);
        });
        Assert.Equal(["Ana", "Luis"], result.Members.Select(member => member.Name));
    }

    [Fact]
    public async Task Summary_OpenIntervalIsReportedApartAndDoesNotCount()
    {
        SeedUser(LuisId, "Luis", MartaId);
        Seed(LuisId, TimeEntryType.Break, Day, new(9, 0), new(9, 10));
        SeedOpen(LuisId, TimeEntryType.Break, new DateOnly(2026, 10, 18), new(11, 0));

        var result = await RunAsync();

        var luis = Assert.Single(result.Members);
        Assert.Equal(10, luis.BreakMinutes);
        Assert.Equal(1, luis.BreakCount);
        Assert.NotNull(luis.Open);
        Assert.Equal(TimeEntryType.Break, luis.Open.Type);
        Assert.Equal(60, luis.Open.ElapsedMinutes);
    }

    [Fact]
    public async Task Summary_OnlyIncludesDirectReportsOfTheCurrentUser()
    {
        SeedUser(MartaId, "Marta", null);
        SeedUser(LuisId, "Luis", MartaId);
        SeedUser(OtherManagerId, "Carlos", null);
        SeedUser(ForeignId, "Eva", OtherManagerId);
        Seed(MartaId, TimeEntryType.Break, Day, new(9, 0), new(10, 0));
        Seed(ForeignId, TimeEntryType.Break, Day, new(9, 0), new(11, 0));
        Seed(LuisId, TimeEntryType.Break, Day, new(9, 0), new(9, 10));

        var result = await RunAsync();

        Assert.Equal([LuisId], result.Members.Select(member => member.MemberId));
        Assert.Equal(10, result.Averages.BreakMinutes);
        Assert.Equal(1, result.MemberCount);
    }

    [Fact]
    public async Task Summary_ForAnEmptyTeam_ReturnsZeroAveragesAndNoQueries()
    {
        SeedUser(MartaId, "Marta", null);

        var result = await RunAsync();

        Assert.Empty(result.Members);
        Assert.Equal(0, result.MemberCount);
        Assert.Equal(0, result.Averages.BreakMinutes);
        Assert.Equal(0, _entries.ListInWindowCalls);
    }

    [Fact]
    public async Task Summary_IgnoresEntriesOutsideTheRangeAndQueriesTheWindowWithTheCap()
    {
        SeedUser(LuisId, "Luis", MartaId);
        Seed(LuisId, TimeEntryType.Break, new DateOnly(2026, 10, 5), new(9, 0), new(10, 0));

        var result = await RunAsync();

        Assert.Equal(0, result.Members.Single().BreakMinutes);
        Assert.Equal(0, result.Members.Single().BreakCount);
        Assert.Equal(GetTimeEntriesUseCase.MaxWindowEntries, _entries.LastRequestedLimit);
        Assert.Equal(Week().ToWindow(), _entries.LastRequestedWindow);
    }

    [Fact]
    public async Task Summary_WithMoreMembersThanTheCap_IsTruncatedAndQueriesOnlyTheCap()
    {
        for (var i = 0; i < GetTeamSummaryUseCase.MaxMembers + 3; i++)
        {
            SeedUser(Guid.NewGuid(), $"Miembro {i:000}", MartaId);
        }

        var result = await RunAsync();

        Assert.True(result.MembersTruncated);
        Assert.Equal(GetTeamSummaryUseCase.MaxMembers, result.MemberCount);
        Assert.Equal(GetTeamSummaryUseCase.MaxMembers, result.Members.Count);
        Assert.Equal(GetTeamSummaryUseCase.MaxMembers, _entries.ListInWindowCalls);
    }

    [Fact]
    public async Task Summary_WithExactlyTheCapOfMembers_IsNotTruncated()
    {
        for (var i = 0; i < GetTeamSummaryUseCase.MaxMembers; i++)
        {
            SeedUser(Guid.NewGuid(), $"Miembro {i:000}", MartaId);
        }

        var result = await RunAsync();

        Assert.False(result.MembersTruncated);
    }
}
