using Microsoft.JSInterop;

using TimeTracker.Web.Tests.Support;
using TimeTracker.Web.Time;

namespace TimeTracker.Web.Tests;

public class BrowserTimeZoneTests
{
    [Fact]
    public void OffsetAt_AsksTheBrowserForTheOffsetOfThatInstant()
    {
        var js = new FakeInProcessRuntime { OffsetMinutes = 120 };
        var instant = new DateTimeOffset(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);

        var offset = new BrowserTimeZone(js).OffsetAt(instant);

        Assert.Equal(TimeSpan.FromHours(2), offset);
        Assert.Equal("timeTrackerLocalTime.offsetMinutes", js.LastIdentifier);
        Assert.Equal(instant.ToUnixTimeMilliseconds(), Assert.IsType<long>(js.LastArguments![0]));
    }

    [Fact]
    public void OffsetAt_AcceptsFractionalMinutes()
    {
        var js = new FakeInProcessRuntime { OffsetMinutes = -210.5 };

        Assert.Equal(TimeSpan.FromMinutes(-210.5), new BrowserTimeZone(js).OffsetAt(DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void StartOfDay_ConvertsTheBrowsersMidnightToUtc()
    {
        var expected = new DateTimeOffset(2026, 9, 30, 22, 0, 0, TimeSpan.Zero);
        var js = new FakeInProcessRuntime { StartOfDayMilliseconds = expected.ToUnixTimeMilliseconds() };

        var start = new BrowserTimeZone(js).StartOfDay(new DateOnly(2026, 10, 1));

        Assert.Equal(expected, start);
        Assert.Equal(TimeSpan.Zero, start.Offset);
        Assert.Equal("timeTrackerLocalTime.startOfDay", js.LastIdentifier);
        Assert.Equal([2026, 10, 1], js.LastArguments!.Cast<int>());
    }

    [Fact]
    public void DateOf_UsesTheLocalDayOfTheInstant()
    {
        var zone = FixedOffsetTimeZone.Utc;

        Assert.Equal(new DateOnly(2026, 10, 12), zone.DateOf(new DateTimeOffset(2026, 10, 12, 23, 59, 59, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 10, 13), new MadridTimeZone().DateOf(new DateTimeOffset(2026, 10, 12, 22, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 10, 12), new MadridTimeZone().DateOf(new DateTimeOffset(2026, 10, 12, 21, 59, 59, TimeSpan.Zero)));
    }

    private sealed class FakeInProcessRuntime : IJSInProcessRuntime
    {
        public double OffsetMinutes { get; init; }

        public long StartOfDayMilliseconds { get; init; }

        public string? LastIdentifier { get; private set; }

        public object?[]? LastArguments { get; private set; }

        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            LastIdentifier = identifier;
            LastArguments = args;
            return identifier switch
            {
                "timeTrackerLocalTime.offsetMinutes" => (TValue)(object)OffsetMinutes,
                "timeTrackerLocalTime.startOfDay" => (TValue)(object)(double)StartOfDayMilliseconds,
                _ => throw new NotSupportedException(identifier),
            };
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new NotSupportedException();
    }
}
