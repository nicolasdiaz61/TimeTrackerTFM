using TimeTracker.Application.TimeTracking;

namespace TimeTracker.Application.Tests.TimeTracking;

public class LocalDateRangeTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    [Fact]
    public void TryCreate_WithAWeekEndingToday_Succeeds()
    {
        var created = LocalDateRange.TryCreate(new DateOnly(2026, 10, 12), Today, Today, out var range, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.Equal(new DateOnly(2026, 10, 12), range!.From);
        Assert.Equal(Today, range.To);
    }

    [Fact]
    public void TryCreate_WithEndBeforeStart_ReturnsAnOrderError()
    {
        var created = LocalDateRange.TryCreate(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 11), Today, out var range, out var error);

        Assert.False(created);
        Assert.Null(range);
        Assert.Equal("La fecha de fin no puede ser anterior a la de inicio.", error);
    }

    [Fact]
    public void TryCreate_WithASingleDay_Succeeds()
        => Assert.True(LocalDateRange.TryCreate(Today, Today, Today, out _, out _));

    [Fact]
    public void TryCreate_With31Days_Succeeds()
        => Assert.True(LocalDateRange.TryCreate(Today.AddDays(-30), Today, Today, out _, out _));

    [Fact]
    public void TryCreate_With32Days_ReturnsATooLongError()
    {
        var created = LocalDateRange.TryCreate(Today.AddDays(-31), Today, Today, out var range, out var error);

        Assert.False(created);
        Assert.Null(range);
        Assert.Equal("El periodo no puede superar los 31 días.", error);
    }

    [Fact]
    public void TryCreate_WithAStartInTheFuture_ReturnsAFuturePeriodError()
    {
        var created = LocalDateRange.TryCreate(Today.AddDays(1), Today.AddDays(2), Today, out var range, out var error);

        Assert.False(created);
        Assert.Null(range);
        Assert.Equal("No se pueden consultar periodos futuros.", error);
    }

    [Fact]
    public void TryCreate_WithAFutureEnd_ClipsItToToday()
    {
        var created = LocalDateRange.TryCreate(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25), Today, out var range, out _);

        Assert.True(created);
        Assert.Equal(Today, range!.To);
    }

    [Fact]
    public void TryCreate_WithAFutureEnd_ClipsBeforeCountingTheDays()
    {
        // 31 días reales (hoy-30..hoy) aunque el fin pedido sea hoy+10.
        Assert.True(LocalDateRange.TryCreate(Today.AddDays(-30), Today.AddDays(10), Today, out var range, out _));
        Assert.Equal(Today, range!.To);

        // hoy-31..hoy son 32 días: el recorte no lo salva.
        Assert.False(LocalDateRange.TryCreate(Today.AddDays(-31), Today.AddDays(10), Today, out _, out var error));
        Assert.Equal("El periodo no puede superar los 31 días.", error);
    }

    [Fact]
    public void ToWindow_IsFromLocalMidnightOfStartToLocalMidnightAfterEnd()
    {
        LocalDateRange.TryCreate(new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 15), out var range, out _);

        var window = range!.ToWindow();

        Assert.Equal(new DateTimeOffset(2026, 7, 13, 22, 0, 0, TimeSpan.Zero), window.From);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 22, 0, 0, TimeSpan.Zero), window.To);
    }

    [Fact]
    public void ToWindow_OfAn31DayPeriodWithTheOctoberChange_Spans31DaysPlusOneHour()
    {
        var today = new DateOnly(2026, 11, 5);
        LocalDateRange.TryCreate(new DateOnly(2026, 10, 6), today, today, out var range, out _);

        var window = range!.ToWindow();

        Assert.Equal(TimeSpan.FromDays(31) + TimeSpan.FromHours(1), window.To - window.From);
    }

    [Fact]
    public void ToWindow_OfAn31DayPeriodWithTheMarchChange_Spans31DaysMinusOneHour()
    {
        var today = new DateOnly(2026, 4, 5);
        LocalDateRange.TryCreate(new DateOnly(2026, 3, 6), today, today, out var range, out _);

        var window = range!.ToWindow();

        Assert.Equal(TimeSpan.FromDays(31) - TimeSpan.FromHours(1), window.To - window.From);
    }
}
