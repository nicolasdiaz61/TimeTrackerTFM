using TimeTracker.Application.TimeTracking;

namespace TimeTracker.Application.Tests;

public class TimeWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MonthStart = new(2026, 9, 30, 22, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MonthEnd = new(2026, 10, 31, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryCreate_WithAMonthStartingInThePast_Succeeds()
    {
        var created = TimeWindow.TryCreate(MonthStart, MonthEnd, Now, out var window, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.Equal(MonthStart, window!.From);
        Assert.Equal(MonthEnd, window.To);
    }

    [Fact]
    public void TryCreate_WithAFutureEnd_Succeeds()
        => Assert.True(TimeWindow.TryCreate(Now.AddHours(-1), Now.AddDays(10), Now, out _, out _));

    [Fact]
    public void TryCreate_StartingExactlyNow_Succeeds()
        => Assert.True(TimeWindow.TryCreate(Now, Now.AddHours(1), Now, out _, out _));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void TryCreate_WhenEndIsNotAfterStart_Fails(int minutesAfterStart)
    {
        var created = TimeWindow.TryCreate(MonthStart, MonthStart.AddMinutes(minutesAfterStart), Now, out var window, out var error);

        Assert.False(created);
        Assert.Null(window);
        Assert.Equal("El fin del periodo debe ser posterior al inicio.", error);
    }

    [Fact]
    public void TryCreate_WithExactlyTheMaximumSpan_Succeeds()
        => Assert.True(TimeWindow.TryCreate(MonthStart, MonthStart.AddDays(TimeWindow.MaxDays), Now, out _, out _));

    [Fact]
    public void TryCreate_WithMoreThanTheMaximumSpan_Fails()
    {
        var created = TimeWindow.TryCreate(MonthStart, MonthStart.AddDays(TimeWindow.MaxDays).AddSeconds(1), Now, out _, out var error);

        Assert.False(created);
        Assert.Equal("El periodo no puede superar los 32 días.", error);
    }

    [Fact]
    public void TryCreate_StartingInTheFuture_Fails()
    {
        var created = TimeWindow.TryCreate(Now.AddSeconds(1), Now.AddDays(1), Now, out _, out var error);

        Assert.False(created);
        Assert.Equal("No se pueden consultar periodos futuros.", error);
    }

    [Fact]
    public void TryCreate_NormalizesToUtc()
    {
        var plusTwo = TimeSpan.FromHours(2);
        TimeWindow.TryCreate(MonthStart.ToOffset(plusTwo), MonthEnd.ToOffset(plusTwo), Now, out var window, out _);

        Assert.Equal(TimeSpan.Zero, window!.From.Offset);
        Assert.Equal(TimeSpan.Zero, window.To.Offset);
        Assert.Equal(MonthStart, window.From);
    }
}
