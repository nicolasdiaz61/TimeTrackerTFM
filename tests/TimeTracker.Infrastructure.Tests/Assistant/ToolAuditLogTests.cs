using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public class ToolAuditLogTests
{
    private static readonly DateOnly Day = new(2026, 10, 15);

    [Fact]
    public async Task TeamMemberTool_LogsToolEmployeeOutcomeAndDuration()
    {
        var harness = new ToolHarness(ToolHarness.Marta, UserRole.Manager);

        await harness.InvokeAsync(
            "get_team_member_time_entries",
            ("memberId", ToolHarness.Luis.ToString()),
            ("fromDate", "2026-10-12"),
            ("toDate", "2026-10-18"));

        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Contains("get_team_member_time_entries", entry.Message, StringComparison.Ordinal);
        Assert.Contains(ToolHarness.Marta.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains(": ok en ", entry.Message, StringComparison.Ordinal);
        Assert.EndsWith(" ms", entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("00000000-0000-4000-8000-00000000000d", "2026-10-12", "2026-10-18", "not-found")]
    [InlineData("00000000-0000-4000-8000-00000000000c", "18/10/2026", "2026-10-18", "invalid")]
    [InlineData("00000000-0000-4000-8000-00000000000c", "2026-10-12", "2026-10-18", "ok")]
    public async Task Outcome_IsOkInvalidOrNotFound(string memberId, string from, string to, string expected)
    {
        var harness = new ToolHarness(ToolHarness.Marta, UserRole.Manager);

        await harness.InvokeAsync("get_team_member_time_entries", ("memberId", memberId), ("fromDate", from), ("toDate", to));

        Assert.Contains($": {expected} en ", Assert.Single(harness.Logger.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedFailure_IsLoggedAsErrorWithoutExceptionData()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee, new ThrowingTimeEntryRepository());

        await harness.InvokeAsync("get_my_time_entries", ("fromDate", "2026-10-12"), ("toDate", "2026-10-18"));

        var message = Assert.Single(harness.Logger.Entries).Message;
        Assert.Contains(": error en ", message, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-secreta-prueba", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Log_NeverContainsMemberIdNamesOrHours()
    {
        var harness = new ToolHarness(ToolHarness.Marta, UserRole.Manager);
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 17), new(12, 43));

        await harness.InvokeAsync("get_team_members");
        await harness.InvokeAsync(
            "get_team_member_time_entries",
            ("memberId", ToolHarness.Luis.ToString()),
            ("fromDate", "2026-10-12"),
            ("toDate", "2026-10-18"));
        await harness.InvokeAsync(
            "get_team_member_time_entries",
            ("memberId", ToolHarness.Pablo.ToString()),
            ("fromDate", "2026-10-12"),
            ("toDate", "2026-10-18"));

        var log = string.Join('\n', harness.Logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain(ToolHarness.Luis.ToString(), log, StringComparison.Ordinal);
        Assert.DoesNotContain(ToolHarness.Pablo.ToString(), log, StringComparison.Ordinal);
        Assert.DoesNotContain("Luis", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Pablo", log, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-10-1", log, StringComparison.Ordinal);
        Assert.DoesNotContain("08:17", log, StringComparison.Ordinal);
    }
}
