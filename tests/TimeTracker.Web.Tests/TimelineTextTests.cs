using TimeTracker.Web.Tests.Support;
using TimeTracker.Web.Timeline;

using static TimeTracker.Web.Tests.Support.Entries;

namespace TimeTracker.Web.Tests;

public class TimelineTextTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(8 * 3600, "08:00")]
    [InlineData((13 * 3600) + (2 * 60) + 59, "13:02")]
    [InlineData(86400, "00:00")]
    public void TimeOfDay_IsHoursAndMinutes(int second, string expected)
        => Assert.Equal(expected, TimelineText.TimeOfDay(second));

    [Fact]
    public void SegmentText_OfAClosedSegmentGivesKindRangeAndSpokenDuration()
    {
        var segment = new TimelineSegment(SegmentKind.MealBreak, 13 * 3600, 14 * 3600, 3600, IsOpen: false);

        Assert.Equal("Comida de 13:00 a 14:00, 1 hora", TimelineText.SegmentText(segment));
    }

    [Fact]
    public void SegmentText_OfAnOpenSegmentSaysItIsInProgress()
    {
        var segment = new TimelineSegment(SegmentKind.Work, 9 * 3600, 12 * 3600, (3 * 3600) + (5 * 60), IsOpen: true);

        Assert.Equal("Trabajo en curso desde las 09:00, 3 horas y 5 minutos", TimelineText.SegmentText(segment));
    }

    [Fact]
    public void SegmentText_OfAVeryShortSegmentSaysLessThanAMinute()
    {
        var segment = new TimelineSegment(SegmentKind.Break, 10 * 3600, (10 * 3600) + 30, 30, IsOpen: false);

        Assert.Equal("Pausa de 10:00 a 10:00, menos de un minuto", TimelineText.SegmentText(segment));
    }

    [Fact]
    public void DayText_ListsTheSegmentsAndTheTotals()
    {
        var day = DayTimelineBuilder.Build(
            new DateOnly(2026, 10, 12),
            [
                Work(Utc(2026, 10, 12, 6), Utc(2026, 10, 12, 11)),
                Meal(Utc(2026, 10, 12, 11), Utc(2026, 10, 12, 12)),
                Work(Utc(2026, 10, 12, 12), Utc(2026, 10, 12, 15, 30)),
            ],
            new FixedOffsetTimeZone(TimeSpan.Zero),
            Utc(2026, 10, 20));

        Assert.Equal(
            "lunes 12: Trabajo de 06:00 a 11:00, 5 horas; Comida de 11:00 a 12:00, 1 hora; Trabajo de 12:00 a 15:30, 3 horas y 30 minutos. "
            + "Trabajado 8 horas y 30 minutos, pausas 0 minutos, comidas 1 hora.",
            TimelineText.DayText(day));
    }

    [Fact]
    public void DayText_OfADayWithoutEntriesSaysSo()
    {
        var day = DayTimelineBuilder.Build(new DateOnly(2026, 10, 10), [], new FixedOffsetTimeZone(TimeSpan.Zero), Utc(2026, 10, 20));

        Assert.Equal("sábado 10: sin fichajes.", TimelineText.DayText(day));
    }

    [Fact]
    public void ShortTotals_ReadWorkedBreaksAndMeals()
    {
        var day = DayTimelineBuilder.Build(
            new DateOnly(2026, 10, 12),
            [Work(Utc(2026, 10, 12, 6), Utc(2026, 10, 12, 13, 30)), Break(Utc(2026, 10, 12, 13, 30), Utc(2026, 10, 12, 13, 45))],
            new FixedOffsetTimeZone(TimeSpan.Zero),
            Utc(2026, 10, 20));

        Assert.Equal("7 h 30 min", TimelineText.WorkedLabel(day));
        Assert.Equal("Pausas 15 min · Comidas 0 min", TimelineText.OthersLabel(day));
    }
}
