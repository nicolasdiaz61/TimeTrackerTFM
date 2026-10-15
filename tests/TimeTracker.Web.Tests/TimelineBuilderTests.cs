using TimeTracker.Web.Calendar;
using TimeTracker.Web.Tests.Support;
using TimeTracker.Web.TimeEntries;
using TimeTracker.Web.Timeline;

using static TimeTracker.Web.Tests.Support.Entries;

namespace TimeTracker.Web.Tests;

public class TimelineBuilderTests
{
    private static readonly MadridTimeZone Madrid = new();
    private static readonly MonthRef October = new(2026, 10);

    // 14 de octubre de 2026, 12:00 hora local de Madrid (UTC+2).
    private static readonly DateTimeOffset Now = Utc(2026, 10, 14, 10, 0);

    private static MonthTimeline Build(MonthRef month, DateTimeOffset now, params TimeEntryDto[] items)
        => MonthTimelineBuilder.Build(month, items, Madrid, now);

    private static DayTimeline Day(MonthTimeline timeline, int day)
        => timeline.Days.Single(candidate => candidate.Date.Day == day);

    [Fact]
    public void Build_GroupsEntriesByTheUsersLocalDay()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 12, 6), Utc(2026, 10, 12, 11, 30)));

        var day = Day(timeline, 12);
        var segment = Assert.Single(day.Segments);
        Assert.Equal(SegmentKind.Work, segment.Kind);
        Assert.Equal(8 * 3600, segment.StartSecond);
        Assert.Equal((13 * 3600) + (30 * 60), segment.EndSecond);
        Assert.Equal((5 * 3600) + (30 * 60), segment.DurationSeconds);
        Assert.False(segment.IsOpen);
        Assert.Equal(330, day.WorkedMinutes);
    }

    [Fact]
    public void Build_AnEveningUtcEntryBelongsToTheNextLocalDay()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 12, 22, 30), Utc(2026, 10, 12, 23, 30)));

        Assert.Empty(Day(timeline, 12).Segments);
        var segment = Assert.Single(Day(timeline, 13).Segments);
        Assert.Equal(30 * 60, segment.StartSecond);
        Assert.Equal(90 * 60, segment.EndSecond);
    }

    [Fact]
    public void Build_SplitsAnEntryThatCrossesMidnightBetweenTwoDays()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 12, 20), Utc(2026, 10, 13, 0)));

        var first = Assert.Single(Day(timeline, 12).Segments);
        var second = Assert.Single(Day(timeline, 13).Segments);
        Assert.Equal((22 * 3600, 86400, 7200), (first.StartSecond, first.EndSecond, first.DurationSeconds));
        Assert.Equal((0, 2 * 3600, 7200), (second.StartSecond, second.EndSecond, second.DurationSeconds));
        Assert.Equal(240, timeline.WorkedMinutes);
    }

    [Fact]
    public void Build_AnEntryStartedInThePreviousMonthOnlyCountsItsPartInsideTheMonth()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 9, 30, 21), Utc(2026, 9, 30, 23)));

        var segment = Assert.Single(Day(timeline, 1).Segments);
        Assert.Equal((0, 3600, 3600), (segment.StartSecond, segment.EndSecond, segment.DurationSeconds));
        Assert.Equal(60, timeline.WorkedMinutes);
    }

    [Fact]
    public void Build_AnEntryRunningPastTheEndOfTheMonthOnlyCountsItsPartInsideTheMonth()
    {
        var timeline = Build(new MonthRef(2026, 9), Now, Work(Utc(2026, 9, 30, 20), Utc(2026, 9, 30, 23)));

        var segment = Assert.Single(Day(timeline, 30).Segments);
        Assert.Equal((22 * 3600, 86400, 7200), (segment.StartSecond, segment.EndSecond, segment.DurationSeconds));
    }

    [Fact]
    public void Build_IgnoresEntriesOutsideTheMonth()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 8, 5, 8), Utc(2026, 8, 5, 9)), Work(Utc(2026, 11, 2, 8), Utc(2026, 11, 2, 9)));

        Assert.Equal(0, timeline.DaysWithEntries);
        Assert.Equal(0, timeline.WorkedMinutes);
    }

    [Fact]
    public void Build_ListsEveryDayOfAPastMonthNewestFirst_EvenAcrossTheClockChange()
    {
        var timeline = Build(October, Utc(2026, 11, 10, 9));

        Assert.Equal(31, timeline.Days.Count);
        Assert.Equal(Enumerable.Range(1, 31).Reverse(), timeline.Days.Select(day => day.Date.Day));
        Assert.Equal(31, timeline.Days.Select(day => day.Date).Distinct().Count());
    }

    [Fact]
    public void Build_InTheCurrentMonthDoesNotListFutureDays()
    {
        var timeline = Build(October, Now);

        Assert.Equal(14, timeline.Days.Count);
        Assert.Equal(14, timeline.Days[0].Date.Day);
        Assert.True(timeline.Days[0].IsToday);
        Assert.All(timeline.Days.Skip(1), day => Assert.False(day.IsToday));
    }

    [Fact]
    public void Build_ForAFutureMonthListsNothing()
    {
        var timeline = Build(new MonthRef(2026, 11), Now);

        Assert.Empty(timeline.Days);
        Assert.True(timeline.IsEmpty);
    }

    [Fact]
    public void Build_MarksWeekends()
    {
        var timeline = Build(October, Now);

        Assert.True(Day(timeline, 10).IsWeekend);
        Assert.True(Day(timeline, 11).IsWeekend);
        Assert.False(Day(timeline, 9).IsWeekend);
        Assert.False(Day(timeline, 12).IsWeekend);
    }

    [Fact]
    public void Build_DrawsTheOpenEntryOfTodayUpToNow()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 14, 7), null));

        var today = Day(timeline, 14);
        var segment = Assert.Single(today.Segments);
        Assert.True(segment.IsOpen);
        Assert.Equal((9 * 3600, 12 * 3600, 3 * 3600), (segment.StartSecond, segment.EndSecond, segment.DurationSeconds));
        Assert.Equal(180, today.WorkedMinutes);
        Assert.Equal(180, timeline.WorkedMinutes);
    }

    [Fact]
    public void Build_AnOpenEntryFromYesterdayFillsYesterdayAndRunsUntilNowToday()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 13, 20), null));

        var yesterday = Assert.Single(Day(timeline, 13).Segments);
        var today = Assert.Single(Day(timeline, 14).Segments);
        Assert.Equal((22 * 3600, 86400), (yesterday.StartSecond, yesterday.EndSecond));
        Assert.Equal((0, 12 * 3600), (today.StartSecond, today.EndSecond));
        Assert.True(yesterday.IsOpen);
        Assert.True(today.IsOpen);
    }

    [Fact]
    public void Build_IgnoresAnEntryThatStartsAfterNow()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 14, 11), null));

        Assert.Equal(0, timeline.DaysWithEntries);
    }

    [Fact]
    public void Build_TotalsEachKindFromTheClippedSegments()
    {
        var timeline = Build(
            October,
            Now,
            Work(Utc(2026, 10, 12, 6), Utc(2026, 10, 12, 11)),
            Break(Utc(2026, 10, 12, 11), Utc(2026, 10, 12, 11, 15)),
            Meal(Utc(2026, 10, 12, 11, 15), Utc(2026, 10, 12, 12, 5)),
            Work(Utc(2026, 10, 12, 12, 5), Utc(2026, 10, 12, 15)),
            Work(Utc(2026, 10, 13, 6), Utc(2026, 10, 13, 7)));

        var day = Day(timeline, 12);
        Assert.Equal((300 + 175, 15, 50), (day.WorkedMinutes, day.BreakMinutes, day.MealMinutes));
        Assert.Equal(300 + 175 + 60, timeline.WorkedMinutes);
        Assert.Equal(15, timeline.BreakMinutes);
        Assert.Equal(50, timeline.MealMinutes);
        Assert.Equal(2, timeline.DaysWithEntries);
        Assert.False(timeline.IsEmpty);
    }

    [Fact]
    public void Build_DayMinutesAreTheSumOfTheMinutesOfItsSegments()
    {
        // Tramos de 90 segundos: cada uno vale 1 minuto (se trunca por tramo), no 1,5.
        var timeline = Build(
            October,
            Now,
            Work(Utc(2026, 10, 12, 6, 0, 0), Utc(2026, 10, 12, 6, 1, 30)),
            Break(Utc(2026, 10, 12, 6, 1, 30), Utc(2026, 10, 12, 6, 3, 0)),
            Work(Utc(2026, 10, 12, 6, 3, 0), Utc(2026, 10, 12, 6, 4, 30)));

        var day = Day(timeline, 12);
        Assert.Equal(2, day.WorkedMinutes);
        Assert.Equal(1, day.BreakMinutes);
        Assert.Equal(180, day.WorkedSeconds);
    }

    [Fact]
    public void Build_AShortSegmentIsKeptEvenWithZeroMinutes()
    {
        var timeline = Build(October, Now, Break(Utc(2026, 10, 12, 10, 0, 0), Utc(2026, 10, 12, 10, 0, 45)));

        var segment = Assert.Single(Day(timeline, 12).Segments);
        Assert.Equal(45, segment.DurationSeconds);
        Assert.Equal(0, segment.DurationMinutes);
        Assert.Equal(1, timeline.DaysWithEntries);
    }

    [Fact]
    public void Build_IgnoresZeroLengthAndUnknownTypeEntries()
    {
        var timeline = Build(
            October,
            Now,
            Work(Utc(2026, 10, 12, 10), Utc(2026, 10, 12, 10)),
            Create("Vacaciones", Utc(2026, 10, 12, 11), Utc(2026, 10, 12, 12)));

        Assert.Equal(0, timeline.DaysWithEntries);
    }

    [Fact]
    public void Build_OrdersSegmentsOfADayByStart()
    {
        var timeline = Build(
            October,
            Now,
            Work(Utc(2026, 10, 12, 12), Utc(2026, 10, 12, 13)),
            Work(Utc(2026, 10, 12, 6), Utc(2026, 10, 12, 7)));

        Assert.Equal([8 * 3600, 14 * 3600], Day(timeline, 12).Segments.Select(segment => segment.StartSecond));
    }

    [Fact]
    public void Build_OnTheDayTheClockGoesBack_UsesWallClockPositionsAndRealDuration()
    {
        // 25 de octubre de 2026: a las 03:00 (CEST) se vuelve a las 02:00 (CET). 00:30Z = 02:30 CEST; 01:30Z = 02:30 CET.
        var timeline = Build(October, Utc(2026, 11, 2, 9), Work(Utc(2026, 10, 25, 0, 30), Utc(2026, 10, 25, 1, 30)));

        var segment = Assert.Single(Day(timeline, 25).Segments);
        Assert.Equal(9000, segment.StartSecond);
        Assert.True(segment.EndSecond >= segment.StartSecond);
        Assert.Equal(3600, segment.DurationSeconds);
    }

    [Fact]
    public void Build_OnTheDayTheClockGoesBack_AMorningEntryKeepsItsLocalHours()
    {
        var timeline = Build(October, Utc(2026, 11, 2, 9), Work(Utc(2026, 10, 25, 5), Utc(2026, 10, 25, 9)));

        var segment = Assert.Single(Day(timeline, 25).Segments);
        Assert.Equal((6 * 3600, 10 * 3600, 4 * 3600), (segment.StartSecond, segment.EndSecond, segment.DurationSeconds));
    }

    [Fact]
    public void Build_OnTheDayTheClockGoesForward_KeepsTheLocalHours()
    {
        // 29 de marzo de 2026: a las 02:00 (CET) se pasa a las 03:00 (CEST). 08:00Z = 10:00 CEST.
        var march = new MonthRef(2026, 3);
        var timeline = Build(march, Utc(2026, 4, 2, 9), Work(Utc(2026, 3, 29, 8), Utc(2026, 3, 29, 10)));

        var segment = Assert.Single(Day(timeline, 29).Segments);
        Assert.Equal((10 * 3600, 12 * 3600, 2 * 3600), (segment.StartSecond, segment.EndSecond, segment.DurationSeconds));
        Assert.Equal(31, timeline.Days.Count);
    }

    [Fact]
    public void Build_UsesTheGivenZoneNotUtc()
    {
        var timeline = MonthTimelineBuilder.Build(
            October,
            [Work(Utc(2026, 10, 12, 6), Utc(2026, 10, 12, 8))],
            new FixedOffsetTimeZone(TimeSpan.FromHours(-5)),
            Now);

        var segment = Assert.Single(timeline.Days.Single(day => day.Date.Day == 12).Segments);
        Assert.Equal(1 * 3600, segment.StartSecond);
    }

    [Fact]
    public void Build_KeepsTheDefaultAxisWhenEverythingFitsInside()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 12, 6), Utc(2026, 10, 12, 15)));

        Assert.Equal(TimelineAxis.Default, timeline.Axis);
    }

    [Fact]
    public void Build_AnEntryEndingExactlyAtTheAxisEndDoesNotWidenIt()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 12, 14), Utc(2026, 10, 12, 20)));

        Assert.Equal(22, timeline.Axis.EndHour);
    }

    [Fact]
    public void Build_WidensTheAxisToWholeHoursForEveryRow()
    {
        var timeline = Build(
            October,
            Now,
            Work(Utc(2026, 10, 12, 3, 20), Utc(2026, 10, 12, 8)),    // 05:20 local
            Work(Utc(2026, 10, 13, 18), Utc(2026, 10, 13, 21, 31))); // acaba a las 23:31 local

        Assert.Equal(new TimelineAxis(5, 24), timeline.Axis);
    }

    [Fact]
    public void Build_AnEntryEndingAtMidnightWidensTheAxisToTwentyFourHours()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 12, 18), Utc(2026, 10, 12, 22)));

        Assert.Equal(24, timeline.Axis.EndHour);
    }

    [Fact]
    public void Build_AnEntryStartingJustAfterMidnightWidensTheAxisDownToZero()
    {
        var timeline = Build(October, Now, Work(Utc(2026, 10, 11, 22, 10), Utc(2026, 10, 12, 4)));

        Assert.Equal(0, timeline.Axis.StartHour);
    }

    [Fact]
    public void Build_TheAxisIsTheDefaultOneInAnEmptyMonth()
        => Assert.Equal(TimelineAxis.Default, Build(October, Now).Axis);
}
