using TimeTracker.Application.TimeTracking;

namespace TimeTracker.Application.Tests.TimeTracking;

public class DurationTextTests
{
    [Theory]
    [InlineData(0, "0 h 0 min")]
    [InlineData(45, "0 h 45 min")]
    [InlineData(60, "1 h 0 min")]
    [InlineData(2342, "39 h 2 min")]
    [InlineData(1866, "31 h 6 min")]
    public void Format_SplitsMinutesIntoHoursAndMinutes(int minutes, string expected)
    {
        Assert.Equal(expected, DurationText.Format(minutes));
    }

    [Fact]
    public void Summary_ExposesPrecomputedTextForTheDaysAndTheTotals()
    {
        var day = new DaySummary(new DateOnly(2026, 10, 14), new TimeOnly(8, 0), new TimeOnly(18, 0), 466, 75, 20);
        var summary = new DailySummaryResult(
            new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), [day], 2342, 75, 20, TimeTrackingStatus.Idle, null, false);

        Assert.Equal("7 h 46 min", day.WorkedText);
        Assert.Equal("1 h 15 min", day.BreakText);
        Assert.Equal("0 h 20 min", day.MealBreakText);
        Assert.Equal("39 h 2 min", summary.WorkedText);
        Assert.Equal("1 h 15 min", summary.BreakText);
        Assert.Equal("0 h 20 min", summary.MealBreakText);
    }
}
