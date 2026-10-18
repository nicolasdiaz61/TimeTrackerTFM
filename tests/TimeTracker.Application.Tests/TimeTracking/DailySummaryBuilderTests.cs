using TimeTracker.Application.Time;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests.TimeTracking;

public class DailySummaryBuilderTests
{
    private static readonly DateOnly Wednesday = new(2026, 10, 14);
    private static readonly DateTimeOffset Now = SpainTime.ToUtc(new DateOnly(2026, 10, 14), new TimeOnly(23, 0));

    [Fact]
    public void Build_SumsClosedIntervalsByTypeInsideTheDay()
    {
        var list = ListOf(
            Closed(TimeEntryType.Work, Wednesday, 8, 0, Wednesday, 9, 0),
            Closed(TimeEntryType.Break, Wednesday, 9, 0, Wednesday, 9, 15),
            Closed(TimeEntryType.Work, Wednesday, 9, 15, Wednesday, 9, 45),
            Closed(TimeEntryType.MealBreak, Wednesday, 14, 0, Wednesday, 14, 20));

        var summary = DailySummaryBuilder.Build(list, RangeOf(Wednesday, Wednesday), Now);

        var day = Assert.Single(summary.Days);
        Assert.Equal(Wednesday, day.Date);
        Assert.Equal(90, day.WorkedMinutes);
        Assert.Equal(15, day.BreakMinutes);
        Assert.Equal(20, day.MealBreakMinutes);
        Assert.Equal(new TimeOnly(8, 0), day.FirstStart);
        Assert.Equal(new TimeOnly(14, 20), day.LastEnd);
        Assert.Equal((90, 15, 20), (summary.WorkedMinutes, summary.BreakMinutes, summary.MealBreakMinutes));
        Assert.Equal((Wednesday, Wednesday), (summary.From, summary.To));
    }

    [Fact]
    public void Build_SplitsAnIntervalCrossingLocalMidnightBetweenBothDays()
    {
        var thursday = Wednesday.AddDays(1);
        var list = ListOf(Closed(TimeEntryType.Work, Wednesday, 23, 0, thursday, 1, 30));

        var summary = DailySummaryBuilder.Build(list, RangeOf(Wednesday, thursday), Now);

        Assert.Equal(2, summary.Days.Count);
        Assert.Equal(60, summary.Days[0].WorkedMinutes);
        Assert.Equal(new TimeOnly(23, 0), summary.Days[0].FirstStart);
        Assert.Equal(new TimeOnly(23, 59), summary.Days[0].LastEnd);
        Assert.Equal(90, summary.Days[1].WorkedMinutes);
        Assert.Equal(new TimeOnly(0, 0), summary.Days[1].FirstStart);
        Assert.Equal(new TimeOnly(1, 30), summary.Days[1].LastEnd);
        Assert.Equal(150, summary.WorkedMinutes);
    }

    [Fact]
    public void Build_AssignsTheLocalDayNotTheUtcDay()
    {
        var july15 = new DateOnly(2026, 7, 15);
        var start = new DateTimeOffset(2026, 7, 14, 22, 30, 0, TimeSpan.Zero);
        var list = ListOf(Result(TimeEntryType.Work, start, start.AddMinutes(30)));

        var summary = DailySummaryBuilder.Build(list, RangeOf(new DateOnly(2026, 7, 14), july15), start.AddHours(2));

        var day = Assert.Single(summary.Days);
        Assert.Equal(july15, day.Date);
        Assert.Equal(new TimeOnly(0, 30), day.FirstStart);
    }

    [Fact]
    public void Build_OnTheOctoberChangeDay_UsesTheOffsetInForceAtEachInstant()
    {
        var change = new DateOnly(2026, 10, 25);
        var list = ListOf(Result(
            TimeEntryType.Work,
            new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.Zero)));

        var summary = DailySummaryBuilder.Build(list, RangeOf(change, change), new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));

        var day = Assert.Single(summary.Days);
        Assert.Equal(90, day.WorkedMinutes);
        Assert.Equal(new TimeOnly(2, 30), day.FirstStart);
        Assert.Equal(new TimeOnly(3, 0), day.LastEnd);
    }

    [Fact]
    public void Build_AnIntervalCoveringThe25HourDay_Counts1500Minutes()
    {
        var change = new DateOnly(2026, 10, 25);
        var list = ListOf(Result(TimeEntryType.Work, SpainTime.StartOfDay(change), SpainTime.StartOfDay(change.AddDays(1))));

        var summary = DailySummaryBuilder.Build(list, RangeOf(change, change), SpainTime.StartOfDay(change.AddDays(2)));

        Assert.Equal(1500, Assert.Single(summary.Days).WorkedMinutes);
    }

    [Fact]
    public void Build_TruncatesEachSegmentToWholeMinutes()
    {
        var start = SpainTime.ToUtc(Wednesday, new TimeOnly(8, 0));
        var list = ListOf(
            Result(TimeEntryType.Work, start, start.AddSeconds(119)),
            Result(TimeEntryType.Work, start.AddHours(1), start.AddHours(1).AddSeconds(59)));

        var summary = DailySummaryBuilder.Build(list, RangeOf(Wednesday, Wednesday), Now);

        Assert.Equal(1, summary.WorkedMinutes);
    }

    [Fact]
    public void Build_ExposesTheOpenIntervalApartAndKeepsItOutOfTheTotals()
    {
        var list = ListOf(
            TimeTrackingStatus.Working,
            Closed(TimeEntryType.Work, Wednesday, 7, 0, Wednesday, 8, 0),
            Open(TimeEntryType.Work, Wednesday, 9, 0));
        var now = SpainTime.ToUtc(Wednesday, new TimeOnly(10, 30));

        var summary = DailySummaryBuilder.Build(list, RangeOf(Wednesday, Wednesday), now);

        Assert.Equal(60, summary.WorkedMinutes);
        Assert.Equal(TimeTrackingStatus.Working, summary.Status);
        Assert.Equal(new OpenEntrySummary(TimeEntryType.Work, Wednesday, new TimeOnly(9, 0), 90), summary.Open);
        Assert.Equal(new TimeOnly(8, 0), Assert.Single(summary.Days).LastEnd);
    }

    [Fact]
    public void Build_WithOnlyAnOpenInterval_HasNoDaysButKeepsTheOpenBlock()
    {
        var list = ListOf(TimeTrackingStatus.OnBreak, Open(TimeEntryType.Break, Wednesday, 9, 0));

        var summary = DailySummaryBuilder.Build(list, RangeOf(Wednesday, Wednesday), SpainTime.ToUtc(Wednesday, new TimeOnly(9, 10)));

        Assert.Empty(summary.Days);
        Assert.Equal(TimeTrackingStatus.OnBreak, summary.Status);
        Assert.Equal(10, summary.Open!.ElapsedMinutes);
    }

    [Fact]
    public void Build_WithoutActivity_HasNoDaysAndIsIdle()
    {
        var summary = DailySummaryBuilder.Build(ListOf(), RangeOf(Wednesday.AddDays(-6), Wednesday), Now);

        Assert.Empty(summary.Days);
        Assert.Equal((0, 0, 0), (summary.WorkedMinutes, summary.BreakMinutes, summary.MealBreakMinutes));
        Assert.Equal(TimeTrackingStatus.Idle, summary.Status);
        Assert.Null(summary.Open);
        Assert.False(summary.Truncated);
    }

    [Fact]
    public void Build_IgnoresThePartOfAnIntervalOutsideTheRange()
    {
        var tuesday = Wednesday.AddDays(-1);
        var list = ListOf(Closed(TimeEntryType.Work, tuesday, 23, 0, Wednesday, 1, 0));

        var summary = DailySummaryBuilder.Build(list, RangeOf(Wednesday, Wednesday), Now);

        var day = Assert.Single(summary.Days);
        Assert.Equal(Wednesday, day.Date);
        Assert.Equal(60, day.WorkedMinutes);
    }

    [Theory]
    [InlineData(GetTimeEntriesUseCase.MaxWindowEntries - 1, false)]
    [InlineData(GetTimeEntriesUseCase.MaxWindowEntries, true)]
    public void Build_IsTruncatedWhenTheListReachesTheWindowLimit(int count, bool expected)
    {
        var items = Enumerable.Range(0, count)
            .Select(_ => Closed(TimeEntryType.Work, Wednesday, 8, 0, Wednesday, 8, 1))
            .ToArray();

        var summary = DailySummaryBuilder.Build(ListOf(items), RangeOf(Wednesday, Wednesday), Now);

        Assert.Equal(expected, summary.Truncated);
    }

    [Fact]
    public void Build_WithHundredsOfIntervals_ReturnsAtMostOneEntryPerDay()
    {
        var first = Wednesday.AddDays(-30);
        var items = Enumerable.Range(0, 31)
            .SelectMany(offset => Enumerable.Range(0, 10).Select(slot =>
                Closed(TimeEntryType.Work, first.AddDays(offset), 6 + slot, 0, first.AddDays(offset), 6 + slot, 30)))
            .ToArray();

        var summary = DailySummaryBuilder.Build(ListOf(items), RangeOf(first, Wednesday), Now);

        Assert.Equal(31, summary.Days.Count);
        Assert.All(summary.Days, day => Assert.Equal(300, day.WorkedMinutes));
        Assert.Equal(31 * 300, summary.WorkedMinutes);
    }

    private static LocalDateRange RangeOf(DateOnly from, DateOnly to)
    {
        Assert.True(LocalDateRange.TryCreate(from, to, to, out var range, out _));
        return range!;
    }

    private static TimeEntryListResult ListOf(params TimeEntryResult[] items) => ListOf(TimeTrackingStatus.Idle, items);

    private static TimeEntryListResult ListOf(TimeTrackingStatus status, params TimeEntryResult[] items)
        => new(items, 0, 0, 0, status);

    private static TimeEntryResult Closed(TimeEntryType type, DateOnly day, int hour, int minute, DateOnly endDay, int endHour, int endMinute)
        => Result(type, SpainTime.ToUtc(day, new TimeOnly(hour, minute)), SpainTime.ToUtc(endDay, new TimeOnly(endHour, endMinute)));

    private static TimeEntryResult Open(TimeEntryType type, DateOnly day, int hour, int minute)
        => Result(type, SpainTime.ToUtc(day, new TimeOnly(hour, minute)), null);

    private static TimeEntryResult Result(TimeEntryType type, DateTimeOffset start, DateTimeOffset? end)
        => new(Guid.NewGuid(), type, start, end, end is null ? null : (int)(end.Value - start).TotalMinutes);
}
