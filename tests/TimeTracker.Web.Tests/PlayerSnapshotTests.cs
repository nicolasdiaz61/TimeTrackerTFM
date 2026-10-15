using TimeTracker.Web.Tests.Support;
using TimeTracker.Web.Timeline;

using static TimeTracker.Web.Tests.Support.Entries;

namespace TimeTracker.Web.Tests;

public class PlayerSnapshotTests
{
    private static readonly MadridTimeZone Madrid = new();

    // 14 de octubre de 2026, 12:00 hora local de Madrid.
    private static readonly DateTimeOffset Now = Utc(2026, 10, 14, 10, 0, 30);

    [Fact]
    public void From_WithoutEntries_IsAllZero()
    {
        var snapshot = PlayerSnapshot.From([], Madrid, Now);

        Assert.Equal(0, snapshot.CurrentSegmentSeconds);
        Assert.Equal(0, snapshot.WorkedTodaySeconds);
    }

    [Fact]
    public void From_WhileWorking_CountsTheCurrentSegmentAndTodaysWork()
    {
        var snapshot = PlayerSnapshot.From(
            [
                Work(Utc(2026, 10, 14, 7), Utc(2026, 10, 14, 8, 30)),
                Break(Utc(2026, 10, 14, 8, 30), Utc(2026, 10, 14, 8, 45)),
                Work(Utc(2026, 10, 14, 8, 45), null),
            ],
            Madrid,
            Now);

        Assert.Equal((75 * 60) + 30, snapshot.CurrentSegmentSeconds);
        Assert.Equal((90 * 60) + (75 * 60) + 30, snapshot.WorkedTodaySeconds);
    }

    [Fact]
    public void From_OnABreak_TheCurrentSegmentIsTheBreakAndWorkStopsCounting()
    {
        var snapshot = PlayerSnapshot.From(
            [Work(Utc(2026, 10, 14, 7), Utc(2026, 10, 14, 9)), Meal(Utc(2026, 10, 14, 9), null)],
            Madrid,
            Now);

        Assert.Equal(3630, snapshot.CurrentSegmentSeconds);
        Assert.Equal(7200, snapshot.WorkedTodaySeconds);
    }

    [Fact]
    public void From_WithAnOpenEntryFromYesterday_CurrentSegmentIsNotClippedButTodaysWorkIs()
    {
        // Desde ayer a las 22:00 locales (20:00Z) hasta ahora (12:00 locales de hoy, 10:00:30Z).
        var snapshot = PlayerSnapshot.From([Work(Utc(2026, 10, 13, 20), null)], Madrid, Now);

        Assert.Equal((14 * 3600) + 30, snapshot.CurrentSegmentSeconds);
        Assert.Equal((12 * 3600) + 30, snapshot.WorkedTodaySeconds);
    }

    [Fact]
    public void From_IgnoresWorkOfOtherDays()
    {
        var snapshot = PlayerSnapshot.From([Work(Utc(2026, 10, 13, 6), Utc(2026, 10, 13, 14))], Madrid, Now);

        Assert.Equal(0, snapshot.WorkedTodaySeconds);
    }

    [Fact]
    public void From_ADesynchronizedClockNeverGivesNegativeTime()
    {
        var snapshot = PlayerSnapshot.From([Work(Utc(2026, 10, 14, 11), null)], Madrid, Now);

        Assert.Equal(0, snapshot.CurrentSegmentSeconds);
        Assert.Equal(0, snapshot.WorkedTodaySeconds);
    }

    [Fact]
    public void From_AdvancesWithTheClock()
    {
        var items = new[] { Work(Utc(2026, 10, 14, 9), null) };

        var first = PlayerSnapshot.From(items, Madrid, Now);
        var later = PlayerSnapshot.From(items, Madrid, Now.AddSeconds(5));

        Assert.Equal(first.CurrentSegmentSeconds + 5, later.CurrentSegmentSeconds);
        Assert.Equal(first.WorkedTodaySeconds + 5, later.WorkedTodaySeconds);
    }
}
