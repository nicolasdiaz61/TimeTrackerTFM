using System.Text.Json;

using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public class MyTimeEntriesToolTests
{
    private const string Tool = "get_my_time_entries";

    private static readonly DateOnly Day = new(2026, 10, 15);

    private static Task<JsonElement> AskAsync(ToolHarness harness, string from, string to)
        => harness.InvokeAsync(Tool, ("fromDate", from), ("toDate", to));

    [Fact]
    public async Task Employee_GetsOnlyOwnTimeEntries()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(12, 0));
        harness.SeedEntry(ToolHarness.Ana, Day, new(9, 0), new(10, 0));

        var result = await AskAsync(harness, "2026-10-12", "2026-10-18");

        Assert.Equal(240, result.GetProperty("workedMinutes").GetInt32());
        Assert.Equal(1, result.GetProperty("days").GetArrayLength());
    }

    [Fact]
    public async Task Manager_GetsOwnTimeEntries_NotTheTeams()
    {
        var harness = new ToolHarness(ToolHarness.Marta, UserRole.Manager);
        harness.SeedEntry(ToolHarness.Marta, Day, new(7, 0), new(9, 0));
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(12, 0));

        var result = await AskAsync(harness, "2026-10-12", "2026-10-18");

        Assert.Equal(120, result.GetProperty("workedMinutes").GetInt32());
    }

    [Fact]
    public async Task ExtraIdentityArguments_AreIgnored()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(12, 0));
        harness.SeedEntry(ToolHarness.Ana, Day, new(9, 0), new(10, 0));

        var result = await harness.InvokeAsync(
            Tool,
            ("fromDate", "2026-10-12"),
            ("toDate", "2026-10-18"),
            ("employeeId", ToolHarness.Ana.ToString()),
            ("memberId", ToolHarness.Ana.ToString()));

        Assert.Equal(240, result.GetProperty("workedMinutes").GetInt32());
    }

    [Fact]
    public async Task SevenDayPeriod_ReturnsTheSummaryByDay()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(12, 0));

        var result = await AskAsync(harness, "2026-10-12", "2026-10-18");

        Assert.Equal("2026-10-12", result.GetProperty("from").GetString());
        Assert.Equal("2026-10-18", result.GetProperty("to").GetString());
        var day = result.GetProperty("days")[0];
        Assert.Equal("2026-10-15", day.GetProperty("date").GetString());
        Assert.Equal(240, day.GetProperty("workedMinutes").GetInt32());
        Assert.Equal("Idle", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task ThirtyOneDays_AreValid_AndThirtyTwoAreRejectedWithoutQuerying()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);

        var valid = await AskAsync(harness, "2026-09-18", "2026-10-18");
        var tooLong = await AskAsync(harness, "2026-09-17", "2026-10-18");

        Assert.False(valid.TryGetProperty("error", out _));
        Assert.Equal("El periodo no puede superar los 31 días.", tooLong.GetProperty("error").GetString());
        Assert.Equal(1, harness.Entries.ListInWindowCalls);
    }

    [Fact]
    public async Task FutureEnd_IsClippedToToday()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);

        var result = await AskAsync(harness, "2026-09-18", "2026-10-28");

        Assert.Equal("2026-10-18", result.GetProperty("to").GetString());
    }

    [Fact]
    public async Task FutureStart_IsRejectedWithoutQueryingData()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);

        var result = await AskAsync(harness, "2026-10-19", "2026-10-20");

        Assert.Equal("No se pueden consultar periodos futuros.", result.GetProperty("error").GetString());
        Assert.Equal(0, harness.Entries.ListInWindowCalls);
    }

    [Theory]
    [InlineData("18/10/2026", "2026-10-18")]
    [InlineData("2026-10-18", "18-10-2026")]
    [InlineData("hoy", "2026-10-18")]
    [InlineData("2026-10-18", "2026-10-17")]
    public async Task InvalidFormatOrOrder_ReturnsAnErrorWithoutQueryingData(string from, string to)
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);

        var result = await AskAsync(harness, from, to);

        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("error").GetString()));
        Assert.Equal(0, harness.Entries.ListInWindowCalls);
    }

    [Fact]
    public async Task UnexpectedRepositoryFailure_ReturnsAGenericMessageWithoutExceptionDetails()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee, new ThrowingTimeEntryRepository());

        var result = await AskAsync(harness, "2026-10-12", "2026-10-18");

        Assert.Equal("No se ha podido completar la consulta en este momento.", result.GetProperty("error").GetString());
        Assert.DoesNotContain("clave-secreta-prueba", result.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", result.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Result_HasNoIntervalList()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);
        harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(12, 0));

        var result = await AskAsync(harness, "2026-10-12", "2026-10-18");

        var text = result.GetRawText();
        Assert.DoesNotContain("items", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clockIn", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clockOut", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListReachingTheWindowLimit_IsMarkedAsTruncated()
    {
        var harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);
        for (var i = 0; i < GetTimeEntriesUseCase.MaxWindowEntries; i++)
        {
            harness.SeedEntry(ToolHarness.Luis, Day, new(8, 0), new(8, 1), TimeEntryType.Break);
        }

        var result = await AskAsync(harness, "2026-10-12", "2026-10-18");

        Assert.True(result.GetProperty("truncated").GetBoolean());
        Assert.Equal(1, result.GetProperty("days").GetArrayLength());
    }
}
