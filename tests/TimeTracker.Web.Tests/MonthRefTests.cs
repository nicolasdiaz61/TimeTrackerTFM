using TimeTracker.Web.Calendar;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests;

public class MonthRefTests
{
    private static readonly MadridTimeZone Madrid = new();

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Constructor_RejectsMonthsOutOfRange(int month)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new MonthRef(2026, month));

    [Fact]
    public void Of_TakesTheMonthOfADate()
        => Assert.Equal(new MonthRef(2026, 10), MonthRef.Of(new DateOnly(2026, 10, 31)));

    [Fact]
    public void Label_IsInSpanish()
        => Assert.Equal("octubre de 2026", new MonthRef(2026, 10).Label);

    [Theory]
    [InlineData(2026, 10, 2026, 9)]
    [InlineData(2026, 1, 2025, 12)]
    public void Previous_MovesBackAndCrossesTheYear(int year, int month, int expectedYear, int expectedMonth)
        => Assert.Equal(new MonthRef(expectedYear, expectedMonth), new MonthRef(year, month).Previous());

    [Theory]
    [InlineData(2026, 9, 2026, 10)]
    [InlineData(2025, 12, 2026, 1)]
    public void Next_MovesForwardAndCrossesTheYear(int year, int month, int expectedYear, int expectedMonth)
        => Assert.Equal(new MonthRef(expectedYear, expectedMonth), new MonthRef(year, month).Next());

    [Fact]
    public void CanGoNext_IsFalseInTheCurrentMonthAndAfterIt()
    {
        var current = new MonthRef(2026, 10);

        Assert.True(new MonthRef(2026, 9).CanGoNext(current));
        Assert.True(new MonthRef(2025, 12).CanGoNext(current));
        Assert.False(current.CanGoNext(current));
        Assert.False(new MonthRef(2026, 11).CanGoNext(current));
    }

    [Fact]
    public void IsCurrent_ComparesWithTheCurrentMonth()
    {
        var current = new MonthRef(2026, 10);

        Assert.True(current.IsCurrent(current));
        Assert.False(new MonthRef(2026, 9).IsCurrent(current));
    }

    [Theory]
    [InlineData(2026, 10, 31)]
    [InlineData(2026, 9, 30)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    public void DaysInMonth_RespectsLeapYears(int year, int month, int expected)
        => Assert.Equal(expected, new MonthRef(year, month).DaysInMonth);

    [Fact]
    public void FirstAndLastDay_AreTheMonthLimits()
    {
        var month = new MonthRef(2026, 10);

        Assert.Equal(new DateOnly(2026, 10, 1), month.FirstDay);
        Assert.Equal(new DateOnly(2026, 10, 31), month.LastDay);
    }

    [Fact]
    public void Window_OfOctoberInMadrid_StartsInSummerTimeAndEndsInWinterTime()
    {
        var (from, to) = new MonthRef(2026, 10).Window(Madrid);

        Assert.Equal(new DateTimeOffset(2026, 9, 30, 22, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero), to);
        Assert.Equal(TimeSpan.FromHours(31 * 24 + 1), to - from);
    }

    [Fact]
    public void Window_OfMarchInMadrid_LosesAnHour()
    {
        var (from, to) = new MonthRef(2026, 3).Window(Madrid);

        Assert.Equal(new DateTimeOffset(2026, 2, 28, 23, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 22, 0, 0, TimeSpan.Zero), to);
        Assert.Equal(TimeSpan.FromHours(31 * 24 - 1), to - from);
    }

    [Fact]
    public void Window_OfDecemberCrossesTheYear()
    {
        var (from, to) = new MonthRef(2026, 12).Window(Madrid);

        Assert.Equal(new DateTimeOffset(2026, 11, 30, 23, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 0, 0, TimeSpan.Zero), to);
    }

    [Fact]
    public void Window_FollowsTheUsersZone()
    {
        var (from, to) = new MonthRef(2026, 10).Window(new FixedOffsetTimeZone(TimeSpan.FromHours(-5)));

        Assert.Equal(new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero), to);
    }

    [Fact]
    public void Window_NeverExceedsTheApiLimitOf32Days()
    {
        foreach (var month in Enumerable.Range(1, 12))
        {
            var (from, to) = new MonthRef(2026, month).Window(Madrid);

            Assert.True(to - from <= TimeSpan.FromDays(32), $"El mes {month} supera 32 días.");
        }
    }
}
