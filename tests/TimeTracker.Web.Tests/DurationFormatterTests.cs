using TimeTracker.Web.Formatting;

namespace TimeTracker.Web.Tests;

public class DurationFormatterTests
{
    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(1, "1 min")]
    [InlineData(45, "45 min")]
    [InlineData(59, "59 min")]
    [InlineData(60, "1 h")]
    [InlineData(65, "1 h 5 min")]
    [InlineData(150, "2 h 30 min")]
    [InlineData(1500, "25 h")]
    public void Compact_UsesHoursAndMinutes(int minutes, string expected)
        => Assert.Equal(expected, DurationFormatter.Compact(minutes));

    [Theory]
    [InlineData(0, "0 minutos")]
    [InlineData(1, "1 minuto")]
    [InlineData(2, "2 minutos")]
    [InlineData(60, "1 hora")]
    [InlineData(61, "1 hora y 1 minuto")]
    [InlineData(65, "1 hora y 5 minutos")]
    [InlineData(120, "2 horas")]
    [InlineData(150, "2 horas y 30 minutos")]
    public void Spoken_UsesCorrectPlurals(int minutes, string expected)
        => Assert.Equal(expected, DurationFormatter.Spoken(minutes));

    [Fact]
    public void Compact_RejectsNegativeMinutes()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DurationFormatter.Compact(-1));

    [Fact]
    public void Spoken_RejectsNegativeMinutes()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DurationFormatter.Spoken(-1));

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(59, "00:00:59")]
    [InlineData(3661, "01:01:01")]
    [InlineData(36000, "10:00:00")]
    [InlineData(90000, "25:00:00")]
    public void Stopwatch_FormatsHoursMinutesAndSeconds(int seconds, string expected)
        => Assert.Equal(expected, DurationFormatter.Stopwatch(seconds));

    [Fact]
    public void Stopwatch_ClampsNegativeValuesToZero()
        => Assert.Equal("00:00:00", DurationFormatter.Stopwatch(-5));
}
