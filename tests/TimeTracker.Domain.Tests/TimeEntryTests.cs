using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Domain.Tests;

public class TimeEntryTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_WithUtcInstant_CreatesOpenEntry()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);

        Assert.Equal(EmployeeId, entry.EmployeeId);
        Assert.Equal(EightAm, entry.ClockInAt);
        Assert.Null(entry.ClockOutAt);
        Assert.True(entry.IsOpen);
        Assert.Equal(7, entry.Id.Version);
    }

    [Fact]
    public void Start_WithNonUtcOffset_Throws()
    {
        var local = new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.FromHours(2));

        Assert.ThrowsAny<ArgumentException>(() => TimeEntry.Start(EmployeeId, local));
    }

    [Fact]
    public void Start_WithEmptyEmployeeId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => TimeEntry.Start(Guid.Empty, EightAm));
    }

    [Fact]
    public void Start_TruncatesToWholeSeconds()
    {
        var withFraction = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero).AddTicks(9_876_543);

        var entry = TimeEntry.Start(EmployeeId, withFraction);

        Assert.Equal(EightAm, entry.ClockInAt);
    }

    [Fact]
    public void Close_AtLaterInstant_ClosesEntry()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);
        var fourPm = EightAm.AddHours(8);

        entry.Close(fourPm);

        Assert.Equal(fourPm, entry.ClockOutAt);
        Assert.False(entry.IsOpen);
    }

    [Fact]
    public void Close_AtSameInstant_IsValid()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);

        entry.Close(EightAm);

        Assert.Equal(EightAm, entry.ClockOutAt);
    }

    [Fact]
    public void Close_BeforeClockIn_Throws()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);

        var exception = Assert.Throws<ClockOutBeforeClockInException>(() => entry.Close(EightAm.AddSeconds(-1)));

        Assert.Equal("Hora de salida no válida", exception.Title);
        Assert.Equal("La hora de salida no puede ser anterior a la de entrada.", exception.Message);
        Assert.True(entry.IsOpen);
    }

    [Fact]
    public void Close_WhenAlreadyClosed_Throws()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);
        var fourPm = EightAm.AddHours(8);
        entry.Close(fourPm);

        var exception = Assert.Throws<TimeEntryAlreadyClosedException>(() => entry.Close(fourPm.AddHours(1)));

        Assert.Equal("El fichaje ya está cerrado", exception.Title);
        Assert.Equal("El fichaje ya tiene registrada la salida.", exception.Message);
        Assert.Equal(fourPm, entry.ClockOutAt);
    }

    [Fact]
    public void Close_WithNonUtcOffset_Throws()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);
        var local = new DateTimeOffset(2026, 10, 12, 18, 0, 0, TimeSpan.FromHours(2));

        Assert.ThrowsAny<ArgumentException>(() => entry.Close(local));
        Assert.True(entry.IsOpen);
    }

    [Fact]
    public void Close_TruncatesToWholeSeconds()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);

        entry.Close(EightAm.AddMilliseconds(900));

        Assert.Equal(EightAm, entry.ClockOutAt);
    }

    [Fact]
    public void CalculateWorked_WhenOpen_ReturnsNull()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);

        Assert.Null(entry.CalculateWorked());
    }

    [Theory]
    [InlineData(16, 30, 0, 510)]
    [InlineData(8, 1, 59, 1)]
    [InlineData(9, 30, 59, 90)]
    public void CalculateWorked_TruncatesPartialMinutes(int hour, int minute, int second, int expectedMinutes)
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);
        entry.Close(new DateTimeOffset(2026, 10, 12, hour, minute, second, TimeSpan.Zero));

        var worked = entry.CalculateWorked();

        Assert.Equal(expectedMinutes, worked?.Minutes);
    }

    [Theory]
    [InlineData(12, 22, 0, 13, 6, 0, 480)]
    [InlineData(12, 23, 30, 13, 0, 45, 75)]
    public void CalculateWorked_AcrossMidnight(int inDay, int inHour, int inMinute, int outDay, int outHour, int outMinute, int expectedMinutes)
    {
        var entry = TimeEntry.Start(EmployeeId, new DateTimeOffset(2026, 10, inDay, inHour, inMinute, 0, TimeSpan.Zero));
        entry.Close(new DateTimeOffset(2026, 10, outDay, outHour, outMinute, 0, TimeSpan.Zero));

        Assert.Equal(expectedMinutes, entry.CalculateWorked()?.Minutes);
    }

    [Fact]
    public void CalculateWorked_UnderOneMinute_ReturnsZero()
    {
        var entry = TimeEntry.Start(EmployeeId, EightAm);
        entry.Close(EightAm.AddSeconds(59));

        Assert.Equal(0, entry.CalculateWorked()?.Minutes);
    }
}
