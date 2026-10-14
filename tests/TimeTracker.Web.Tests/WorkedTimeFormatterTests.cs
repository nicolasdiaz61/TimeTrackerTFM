using TimeTracker.Web.Formatting;

namespace TimeTracker.Web.Tests;

public class WorkedTimeFormatterTests
{
    [Theory]
    [InlineData(0, "0 minutos (0:00)")]
    [InlineData(1, "1 minuto (0:01)")]
    [InlineData(2, "2 minutos (0:02)")]
    [InlineData(59, "59 minutos (0:59)")]
    [InlineData(60, "60 minutos (1:00)")]
    [InlineData(61, "61 minutos (1:01)")]
    [InlineData(90, "90 minutos (1:30)")]
    [InlineData(1500, "1500 minutos (25:00)")]
    public void FormatTotal_UsesSingularOnlyForOneMinute(int minutes, string expected)
        => Assert.Equal(expected, WorkedTimeFormatter.FormatTotal(minutes));
}
