using TimeTracker.Application.DemoData;

namespace TimeTracker.Application.Tests.DemoData;

public class SpainTimeTests
{
    private static readonly TimeOnly EightAm = new(8, 0);

    [Fact]
    public void ToUtc_InWinter_SubtractsOneHour()
        => Assert.Equal(Utc(2026, 1, 15, 7), SpainTime.ToUtc(new DateOnly(2026, 1, 15), EightAm));

    [Fact]
    public void ToUtc_InSummer_SubtractsTwoHours()
        => Assert.Equal(Utc(2026, 7, 15, 6), SpainTime.ToUtc(new DateOnly(2026, 7, 15), EightAm));

    [Fact]
    public void ToUtc_AroundOctoberChange_FridayIsSummerAndMondayIsWinter()
    {
        // El último domingo de octubre de 2026 es el 25.
        Assert.Equal(Utc(2026, 10, 23, 6), SpainTime.ToUtc(new DateOnly(2026, 10, 23), EightAm));
        Assert.Equal(Utc(2026, 10, 26, 7), SpainTime.ToUtc(new DateOnly(2026, 10, 26), EightAm));
    }

    [Fact]
    public void ToUtc_AroundMarchChange_FridayIsWinterAndMondayIsSummer()
    {
        // El último domingo de marzo de 2026 es el 29.
        Assert.Equal(Utc(2026, 3, 27, 7), SpainTime.ToUtc(new DateOnly(2026, 3, 27), EightAm));
        Assert.Equal(Utc(2026, 3, 30, 6), SpainTime.ToUtc(new DateOnly(2026, 3, 30), EightAm));
    }

    [Fact]
    public void ToUtc_OnChangeSunday_UsesTheOffsetInForceAtThatInstant()
    {
        // 25/10/2026: a las 03:30 locales ya es invierno (UTC+1); a las 01:30 aún es verano (UTC+2).
        Assert.Equal(Utc(2026, 10, 25, 2, 30), SpainTime.ToUtc(new DateOnly(2026, 10, 25), new TimeOnly(3, 30)));
        Assert.Equal(Utc(2026, 10, 24, 23, 30), SpainTime.ToUtc(new DateOnly(2026, 10, 25), new TimeOnly(1, 30)));
    }

    [Fact]
    public void ToUtc_MidnightOfALocalDay_IsPreviousDayInUtc()
        => Assert.Equal(Utc(2026, 10, 13, 22), SpainTime.ToUtc(new DateOnly(2026, 10, 14), TimeOnly.MinValue));

    [Fact]
    public void DateOf_LateEveningUtcInSummer_IsNextLocalDay()
        => Assert.Equal(new DateOnly(2026, 10, 14), SpainTime.DateOf(Utc(2026, 10, 13, 22, 30)));

    [Fact]
    public void DateOf_LateEveningUtcInWinter_IsNextLocalDay()
        => Assert.Equal(new DateOnly(2026, 1, 16), SpainTime.DateOf(Utc(2026, 1, 15, 23, 30)));

    [Fact]
    public void DateOf_EarlyMorningUtc_IsSameDay()
        => Assert.Equal(new DateOnly(2026, 10, 14), SpainTime.DateOf(Utc(2026, 10, 14, 5)));

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
