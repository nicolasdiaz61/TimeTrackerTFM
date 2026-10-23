using System.Text.Json;

using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public class TeamSummaryToolTests
{
    private const string Tool = "get_team_summary";
    private static readonly DateOnly Day = new(2026, 10, 15);

    private static ToolHarness Manager() => new(ToolHarness.Marta, UserRole.Manager);

    private static Task<JsonElement> SummaryAsync(ToolHarness harness, string from = "2026-10-12", string to = "2026-10-18")
        => harness.InvokeAsync(Tool, ("fromDate", from), ("toDate", to));

    private static void SeedBreaks(ToolHarness harness, Guid employee, params int[] minutes)
    {
        for (var i = 0; i < minutes.Length; i++)
        {
            harness.SeedEntry(employee, Day, new(9 + i, 0), new TimeOnly(9 + i, 0).AddMinutes(minutes[i]), TimeEntryType.Break);
        }
    }

    [Fact]
    public void Manager_HasTheToolAndEmployeeDoesNot()
    {
        var manager = Manager();
        var employee = new ToolHarness(ToolHarness.Luis, UserRole.Employee);

        Assert.Contains(manager.Tools, tool => tool.Name == Tool);
        Assert.DoesNotContain(employee.Tools, tool => tool.Name == Tool);
    }

    [Fact]
    public void Description_MentionsComparisonsRankingsAndAveragesAndTheDateLimit()
    {
        var description = Manager().Tools.Single(tool => tool.Name == Tool).Description;

        Assert.Contains("comparaciones, rankings y medias", description, StringComparison.Ordinal);
        Assert.Contains("quién hace más pausas", description, StringComparison.Ordinal);
        Assert.Contains("por encima del promedio", description, StringComparison.Ordinal);
        Assert.Contains("máximo 31 días", description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summary_ReturnsPrecomputedMetricsAveragesAndFlagsPerMember()
    {
        var harness = Manager();
        SeedBreaks(harness, ToolHarness.Luis, 20, 20, 20);
        SeedBreaks(harness, ToolHarness.Ana, 10);

        var result = await SummaryAsync(harness);

        Assert.Equal(2, result.GetProperty("memberCount").GetInt32());
        Assert.False(result.GetProperty("membersTruncated").GetBoolean());
        Assert.Equal(35, result.GetProperty("averages").GetProperty("breakMinutes").GetDouble());
        Assert.Equal(2, result.GetProperty("averages").GetProperty("breakCount").GetDouble());
        var luis = result.GetProperty("members")[0];
        Assert.Equal(ToolHarness.Luis.ToString(), luis.GetProperty("memberId").GetString());
        Assert.Equal("Luis Empleado", luis.GetProperty("name").GetString());
        Assert.Equal(60, luis.GetProperty("breakMinutes").GetInt32());
        Assert.Equal("1 h 0 min", luis.GetProperty("breakText").GetString());
        Assert.Equal(3, luis.GetProperty("breakCount").GetInt32());
        Assert.Equal(25, luis.GetProperty("breakMinutesVsAverage").GetDouble());
        Assert.True(luis.GetProperty("aboveAverageBreakMinutes").GetBoolean());
        Assert.True(luis.GetProperty("aboveAverageBreakCount").GetBoolean());
        var ana = result.GetProperty("members")[1];
        Assert.False(ana.GetProperty("aboveAverageBreakMinutes").GetBoolean());
    }

    [Fact]
    public void DailyHours_IsAnOptionalNumberParameterWithADescription_AndDatesStayRequired()
    {
        var schema = Manager().Tools.Single(tool => tool.Name == Tool).JsonSchema;

        var hours = schema.GetProperty("properties").GetProperty("dailyHours");
        Assert.Equal("number", hours.GetProperty("type").GetString());
        Assert.Contains("horas previstas por día laborable", hours.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("usa 8 salvo que el usuario indique otra", hours.GetProperty("description").GetString(), StringComparison.Ordinal);
        var required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.Equal(["fromDate", "toDate"], required.Order());
    }

    [Fact]
    public async Task Overtime_WithoutDailyHours_AssumesEightAndReturnsPrecomputedFields()
    {
        var harness = Manager();
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(18, 30));
        harness.SeedEntry(ToolHarness.Ana, Day, new(8, 0), new(14, 0));

        var result = await SummaryAsync(harness);

        Assert.Equal(8, result.GetProperty("assumedDailyHours").GetDouble());
        Assert.Contains("8 h", result.GetProperty("overtimeNote").GetString(), StringComparison.Ordinal);
        Assert.Equal(75, result.GetProperty("averages").GetProperty("overtimeMinutes").GetDouble());
        Assert.Equal("1 h 15 min", result.GetProperty("averages").GetProperty("overtimeText").GetString());
        var luis = result.GetProperty("members").EnumerateArray().Single(member => member.GetProperty("memberId").GetString() == ToolHarness.Luis.ToString());
        Assert.Equal(150, luis.GetProperty("overtimeMinutes").GetInt32());
        Assert.Equal("2 h 30 min", luis.GetProperty("overtimeText").GetString());
        Assert.Equal(1, luis.GetProperty("overtimeDays").GetInt32());
        Assert.Equal(75, luis.GetProperty("overtimeVsAverage").GetDouble());
        Assert.True(luis.GetProperty("aboveAverageOvertime").GetBoolean());
    }

    [Fact]
    public async Task Overtime_WithDailyHours_UsesTheGivenValue()
    {
        var harness = Manager();
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(18, 30));

        var result = await harness.InvokeAsync(Tool, ("fromDate", "2026-10-12"), ("toDate", "2026-10-18"), ("dailyHours", 6.5));

        Assert.Equal(6.5, result.GetProperty("assumedDailyHours").GetDouble());
        Assert.Equal(240, result.GetProperty("members").EnumerateArray().Single(member => member.GetProperty("memberId").GetString() == ToolHarness.Luis.ToString()).GetProperty("overtimeMinutes").GetInt32());
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.0)]
    [InlineData(13.0)]
    [InlineData(-8.0)]
    public async Task Overtime_WithInvalidDailyHours_ReturnsAnErrorWithoutQueryingAnything(double hours)
    {
        var harness = Manager();

        var result = await harness.InvokeAsync(Tool, ("fromDate", "2026-10-12"), ("toDate", "2026-10-18"), ("dailyHours", hours));

        Assert.Equal(["error"], result.EnumerateObject().Select(property => property.Name));
        Assert.Contains("dailyHours", result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, harness.Entries.ListInWindowCalls);
        Assert.Null(harness.Users.LastLimit);
    }

    [Fact]
    public async Task Overtime_UsesTheAssignedScheduleOfEachMember()
    {
        var harness = Manager();
        var schedule = TimeTracker.Domain.WorkSchedules.WorkSchedule.Create(
            Guid.NewGuid(), Company.DefaultId, new("Fines", new TimeOnly(8, 0), new TimeOnly(13, 0), 45, 15, [DayOfWeek.Saturday]));
        harness.Schedules.Seed(schedule);
        harness.Users.All.Single(user => user.Id == ToolHarness.Luis).AssignWorkSchedule(schedule.Id);
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(10, 0));
        harness.SeedEntry(ToolHarness.Ana, Day, new(8, 0), new(10, 0));

        var result = await SummaryAsync(harness);

        var members = result.GetProperty("members").EnumerateArray().ToDictionary(member => member.GetProperty("memberId").GetString()!);
        Assert.Equal(120, members[ToolHarness.Luis.ToString()].GetProperty("overtimeMinutes").GetInt32());
        Assert.Equal(0, members[ToolHarness.Ana.ToString()].GetProperty("overtimeMinutes").GetInt32());
    }

    [Fact]
    public async Task Summary_ExposesNoEmailsHashesOrManagerData()
    {
        var harness = Manager();
        SeedBreaks(harness, ToolHarness.Luis, 10);

        var text = (await SummaryAsync(harness)).GetRawText();

        Assert.DoesNotContain("example.test", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marta", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Pablo", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summary_DisplayNamesAreSanitized()
    {
        var harness = Manager();
        var longName = "Eva\n  Gil" + new string('x', 90);
        harness.Users.Seed(UserAccount.Create(Guid.NewGuid(), Company.DefaultId, "eva@example.test", longName, "hash", UserRole.Employee, ToolHarness.Marta));

        var result = await SummaryAsync(harness);

        var name = result.GetProperty("members").EnumerateArray()
            .Select(member => member.GetProperty("name").GetString()!)
            .Single(candidate => candidate.StartsWith("Eva", StringComparison.Ordinal));
        Assert.StartsWith("Eva Gil", name, StringComparison.Ordinal);
        Assert.True(name.Length <= 60);
        Assert.DoesNotContain('\n', name);
    }

    [Fact]
    public async Task Summary_OfAManagerWithoutTeam_IsEmpty()
    {
        var harness = new ToolHarness(ToolHarness.Ana, UserRole.Manager);

        var result = await SummaryAsync(harness);

        Assert.Equal(0, result.GetProperty("memberCount").GetInt32());
        Assert.Equal(0, result.GetProperty("members").GetArrayLength());
    }

    [Theory]
    [InlineData("hoy", "2026-10-18")]
    [InlineData("2026-10-12", "ayer")]
    [InlineData("2026-10-18", "2026-10-12")]
    [InlineData("2026-09-01", "2026-10-18")]
    [InlineData("2026-10-19", "2026-10-20")]
    public async Task Summary_WithInvalidDates_ReturnsAnErrorWithoutQueryingAnything(string from, string to)
    {
        var harness = Manager();

        var result = await SummaryAsync(harness, from, to);

        Assert.Equal(["error"], result.EnumerateObject().Select(property => property.Name));
        Assert.Equal(0, harness.Entries.ListInWindowCalls);
        Assert.Null(harness.Users.LastLimit);
    }
}
