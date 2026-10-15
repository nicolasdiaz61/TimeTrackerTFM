using TimeTracker.Web.Timeline;

namespace TimeTracker.Web.Tests;

public class TimelineAxisTests
{
    [Fact]
    public void Default_RunsFromSixToTenPm()
    {
        Assert.Equal(6, TimelineAxis.Default.StartHour);
        Assert.Equal(22, TimelineAxis.Default.EndHour);
    }

    [Fact]
    public void Constructor_RejectsInvalidRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineAxis(-1, 22));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineAxis(6, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineAxis(10, 10));
    }

    [Theory]
    [InlineData(6 * 3600, 0)]
    [InlineData(8 * 3600, 125)]
    [InlineData(14 * 3600, 500)]
    [InlineData(22 * 3600, 1000)]
    public void X_ScalesLinearlyOverTheViewWidth(int second, double expected)
        => Assert.Equal(expected, TimelineAxis.Default.X(second), 6);

    [Fact]
    public void X_IsClampedToTheAxis()
    {
        Assert.Equal(0, TimelineAxis.Default.X(0));
        Assert.Equal(TimelineAxis.ViewWidth, TimelineAxis.Default.X(86400));
    }

    [Fact]
    public void Layout_PlacesASegmentAtScale()
    {
        var segment = new TimelineSegment(SegmentKind.Work, 8 * 3600, 9 * 3600, 3600, IsOpen: false);

        var (x, width) = TimelineAxis.Default.Layout(segment);

        Assert.Equal(125, x, 6);
        Assert.Equal(62.5, width, 6);
    }

    [Fact]
    public void Layout_GivesVeryShortSegmentsAVisibleMinimumWidth()
    {
        var segment = new TimelineSegment(SegmentKind.Break, 12 * 3600, 12 * 3600 + 30, 30, IsOpen: false);

        var (_, width) = TimelineAxis.Default.Layout(segment);

        Assert.Equal(TimelineAxis.MinSegmentWidth, width);
    }

    [Fact]
    public void Layout_NeverOverflowsTheRightEdge()
    {
        var segment = new TimelineSegment(SegmentKind.Work, 22 * 3600 - 10, 22 * 3600, 10, IsOpen: false);

        var (x, width) = TimelineAxis.Default.Layout(segment);

        Assert.True(x + width <= TimelineAxis.ViewWidth + 1e-9);
        Assert.Equal(TimelineAxis.MinSegmentWidth, width, 6);
    }

    [Fact]
    public void Ticks_EveryTwoHoursForTheDefaultAxis()
        => Assert.Equal([6, 8, 10, 12, 14, 16, 18, 20, 22], TimelineAxis.Default.TickHours);

    [Fact]
    public void Ticks_EveryTwoHoursStartOnAnEvenHourInsideTheAxis()
        => Assert.Equal([6, 8, 10, 12, 14, 16, 18, 20, 22], new TimelineAxis(5, 22).TickHours);

    [Fact]
    public void Ticks_EveryThreeHoursForAWideAxis()
        => Assert.Equal([0, 3, 6, 9, 12, 15, 18, 21, 24], new TimelineAxis(0, 24).TickHours);

    [Fact]
    public void Ticks_WideAxisStartingMidMorningKeepsMultiplesOfThree()
        => Assert.Equal([3, 6, 9, 12, 15, 18, 21, 24], new TimelineAxis(2, 24).TickHours);

    [Fact]
    public void Ticks_AreMultiplesOfTheStepInsideTheAxis()
    {
        var axis = new TimelineAxis(7, 22);

        Assert.All(axis.TickHours, hour => Assert.InRange(hour, 7, 22));
        Assert.Equal([8, 10, 12, 14, 16, 18, 20, 22], axis.TickHours);
    }
}
