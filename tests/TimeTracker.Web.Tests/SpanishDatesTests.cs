using TimeTracker.Web.Calendar;

namespace TimeTracker.Web.Tests;

public class SpanishDatesTests
{
    [Theory]
    [InlineData(1, "enero")]
    [InlineData(3, "marzo")]
    [InlineData(9, "septiembre")]
    [InlineData(10, "octubre")]
    [InlineData(12, "diciembre")]
    public void MonthName_IsLowercaseSpanish(int month, string expected)
        => Assert.Equal(expected, SpanishDates.MonthName(month));

    [Fact]
    public void MonthLabel_ReadsLikeOctoberOf2026()
        => Assert.Equal("octubre de 2026", SpanishDates.MonthLabel(2026, 10));

    [Theory]
    [InlineData(2026, 10, 12, "lun 12", "lunes 12")]
    [InlineData(2026, 10, 14, "mié 14", "miércoles 14")]
    [InlineData(2026, 10, 17, "sáb 17", "sábado 17")]
    [InlineData(2026, 10, 18, "dom 18", "domingo 18")]
    [InlineData(2026, 10, 6, "mar 6", "martes 6")]
    [InlineData(2026, 10, 8, "jue 8", "jueves 8")]
    [InlineData(2026, 10, 9, "vie 9", "viernes 9")]
    public void DayLabels_UseSpanishWeekdays(int year, int month, int day, string shortLabel, string longLabel)
    {
        var date = new DateOnly(year, month, day);

        Assert.Equal(shortLabel, SpanishDates.DayLabel(date));
        Assert.Equal(longLabel, SpanishDates.LongDayLabel(date));
    }

    [Theory]
    [InlineData(2026, 10, 17, true)]
    [InlineData(2026, 10, 18, true)]
    [InlineData(2026, 10, 16, false)]
    [InlineData(2026, 10, 12, false)]
    public void IsWeekend_IsTrueOnlyForSaturdayAndSunday(int year, int month, int day, bool expected)
        => Assert.Equal(expected, SpanishDates.IsWeekend(new DateOnly(year, month, day)));
}
