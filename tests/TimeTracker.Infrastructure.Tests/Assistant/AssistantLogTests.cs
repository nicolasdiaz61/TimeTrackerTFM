using Microsoft.Extensions.Logging;

using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public class AssistantLogTests
{
    [Fact]
    public void ToolExecuted_LogsOnlyToolEmployeeOutcomeAndDuration()
    {
        var logger = new CapturingLogger();
        var employeeId = Guid.Parse("3f2b6a52-0000-4000-8000-000000000001");

        AssistantLog.ToolExecuted(logger, "get_team_member_time_entries", employeeId, ToolOutcome.NotFound, 12);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("get_team_member_time_entries", entry.Message, StringComparison.Ordinal);
        Assert.Contains(employeeId.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains("not-found", entry.Message, StringComparison.Ordinal);
        Assert.Contains("12", entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ToolOutcome.Ok, "ok")]
    [InlineData(ToolOutcome.Invalid, "invalid")]
    [InlineData(ToolOutcome.NotFound, "not-found")]
    [InlineData(ToolOutcome.Error, "error")]
    public void ToolOutcome_UsesTheNamesOfTheSpecification(string outcome, string expected)
    {
        Assert.Equal(expected, outcome);
    }
}
