using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Domain.Tests;

public class TimeEntryTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_WithUtcInstant_CreatesOpenEntry()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);

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

        Assert.ThrowsAny<ArgumentException>(() => TimeEntry.Start(EmployeeId, TimeEntryType.Work, local));
    }

    [Fact]
    public void Start_WithEmptyEmployeeId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => TimeEntry.Start(Guid.Empty, TimeEntryType.Work, EightAm));
    }

    [Fact]
    public void Start_TruncatesToWholeSeconds()
    {
        var withFraction = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero).AddTicks(9_876_543);

        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, withFraction);

        Assert.Equal(EightAm, entry.ClockInAt);
    }

    [Fact]
    public void Close_AtLaterInstant_ClosesEntry()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        var fourPm = EightAm.AddHours(8);

        entry.Close(fourPm);

        Assert.Equal(fourPm, entry.ClockOutAt);
        Assert.False(entry.IsOpen);
    }

    [Fact]
    public void Close_AtSameInstant_IsValid()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);

        entry.Close(EightAm);

        Assert.Equal(EightAm, entry.ClockOutAt);
    }

    [Fact]
    public void Close_BeforeClockIn_Throws()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);

        var exception = Assert.Throws<ClockOutBeforeClockInException>(() => entry.Close(EightAm.AddSeconds(-1)));

        Assert.Equal("Hora de salida no válida", exception.Title);
        Assert.Equal("La hora de salida no puede ser anterior a la de entrada.", exception.Message);
        Assert.True(entry.IsOpen);
    }

    [Fact]
    public void Close_WhenAlreadyClosed_Throws()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
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
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        var local = new DateTimeOffset(2026, 10, 12, 18, 0, 0, TimeSpan.FromHours(2));

        Assert.ThrowsAny<ArgumentException>(() => entry.Close(local));
        Assert.True(entry.IsOpen);
    }

    [Fact]
    public void Close_TruncatesToWholeSeconds()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);

        entry.Close(EightAm.AddMilliseconds(900));

        Assert.Equal(EightAm, entry.ClockOutAt);
    }

    [Fact]
    public void CalculateDuration_WhenOpen_ReturnsNull()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);

        Assert.Null(entry.CalculateDuration());
    }

    [Theory]
    [InlineData(16, 30, 0, 510)]
    [InlineData(8, 1, 59, 1)]
    [InlineData(9, 30, 59, 90)]
    public void CalculateDuration_TruncatesPartialMinutes(int hour, int minute, int second, int expectedMinutes)
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        entry.Close(new DateTimeOffset(2026, 10, 12, hour, minute, second, TimeSpan.Zero));

        var worked = entry.CalculateDuration();

        Assert.Equal(expectedMinutes, worked?.Minutes);
    }

    [Theory]
    [InlineData(12, 22, 0, 13, 6, 0, 480)]
    [InlineData(12, 23, 30, 13, 0, 45, 75)]
    public void CalculateDuration_AcrossMidnight(int inDay, int inHour, int inMinute, int outDay, int outHour, int outMinute, int expectedMinutes)
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, new DateTimeOffset(2026, 10, inDay, inHour, inMinute, 0, TimeSpan.Zero));
        entry.Close(new DateTimeOffset(2026, 10, outDay, outHour, outMinute, 0, TimeSpan.Zero));

        Assert.Equal(expectedMinutes, entry.CalculateDuration()?.Minutes);
    }

    [Fact]
    public void CalculateDuration_UnderOneMinute_ReturnsZero()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        entry.Close(EightAm.AddSeconds(59));

        Assert.Equal(0, entry.CalculateDuration()?.Minutes);
    }

    [Theory]
    [InlineData(TimeEntryType.Work)]
    [InlineData(TimeEntryType.Break)]
    [InlineData(TimeEntryType.MealBreak)]
    public void Start_WithEachType_CreatesOpenEntryOfThatType(TimeEntryType type)
    {
        var entry = TimeEntry.Start(EmployeeId, type, EightAm);

        Assert.Equal(type, entry.Type);
        Assert.True(entry.IsOpen);
    }

    [Fact]
    public void Start_WithUndefinedType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeEntry.Start(EmployeeId, (TimeEntryType)99, EightAm));
    }

    [Fact]
    public void CalculateDuration_OfClosedBreak_ReturnsItsMinutes()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Break, EightAm.AddHours(2));
        entry.Close(EightAm.AddHours(2).AddMinutes(15).AddSeconds(59));

        Assert.Equal(15, entry.CalculateDuration()?.Minutes);
    }

    [Fact]
    public void CalculateDuration_OfOpenMealBreak_ReturnsNull()
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.MealBreak, EightAm);

        Assert.Null(entry.CalculateDuration());
    }

    [Theory]
    [InlineData(TimeEntryType.Break)]
    [InlineData(TimeEntryType.MealBreak)]
    public void Pause_FromWork_ClosesWorkAndOpensBreakOfRequestedType(TimeEntryType breakType)
    {
        var work = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        var at = EightAm.AddHours(2);

        var pause = work.Pause(breakType, at);

        Assert.Equal(at, work.ClockOutAt);
        Assert.False(work.IsOpen);
        Assert.Equal(breakType, pause.Type);
        Assert.Equal(at, pause.ClockInAt);
        Assert.True(pause.IsOpen);
        Assert.Equal(EmployeeId, pause.EmployeeId);
        Assert.NotEqual(work.Id, pause.Id);
    }

    [Theory]
    [InlineData(TimeEntryType.Break)]
    [InlineData(TimeEntryType.MealBreak)]
    public void Pause_FromBreak_ThrowsNotWorkingAndKeepsEntryOpen(TimeEntryType current)
    {
        var entry = TimeEntry.Start(EmployeeId, current, EightAm);

        var exception = Assert.Throws<NotWorkingException>(() => entry.Pause(TimeEntryType.Break, EightAm.AddHours(1)));

        Assert.Equal("No está trabajando", exception.Title);
        Assert.True(entry.IsOpen);
    }

    [Theory]
    [InlineData(TimeEntryType.Work)]
    [InlineData((TimeEntryType)99)]
    public void Pause_WithNonBreakType_ThrowsAndKeepsEntryOpen(TimeEntryType invalid)
    {
        var work = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);

        Assert.Throws<ArgumentOutOfRangeException>(() => work.Pause(invalid, EightAm.AddHours(1)));

        Assert.True(work.IsOpen);
    }

    [Fact]
    public void Pause_WhenClockBeforeStart_ThrowsAndKeepsEntryOpen()
    {
        var work = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);

        Assert.Throws<ClockOutBeforeClockInException>(() => work.Pause(TimeEntryType.Break, EightAm.AddSeconds(-1)));

        Assert.True(work.IsOpen);
    }

    [Fact]
    public void Pause_WhenWorkAlreadyClosed_Throws()
    {
        var work = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        work.Close(EightAm.AddHours(1));

        Assert.Throws<TimeEntryAlreadyClosedException>(() => work.Pause(TimeEntryType.Break, EightAm.AddHours(2)));
    }

    [Theory]
    [InlineData(TimeEntryType.Break)]
    [InlineData(TimeEntryType.MealBreak)]
    public void Resume_FromBreak_ClosesBreakAndOpensWork(TimeEntryType current)
    {
        var pause = TimeEntry.Start(EmployeeId, current, EightAm.AddHours(2));
        var at = EightAm.AddHours(2).AddMinutes(15);

        var work = pause.Resume(at);

        Assert.Equal(at, pause.ClockOutAt);
        Assert.Equal(TimeEntryType.Work, work.Type);
        Assert.Equal(at, work.ClockInAt);
        Assert.True(work.IsOpen);
        Assert.Equal(EmployeeId, work.EmployeeId);
    }

    [Fact]
    public void Resume_FromWork_ThrowsNoOpenBreakAndKeepsEntryOpen()
    {
        var work = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);

        var exception = Assert.Throws<NoOpenBreakException>(() => work.Resume(EightAm.AddHours(1)));

        Assert.Equal("No hay ninguna pausa abierta", exception.Title);
        Assert.True(work.IsOpen);
    }

    [Fact]
    public void Resume_WhenClockBeforeStart_ThrowsAndKeepsBreakOpen()
    {
        var pause = TimeEntry.Start(EmployeeId, TimeEntryType.Break, EightAm);

        Assert.Throws<ClockOutBeforeClockInException>(() => pause.Resume(EightAm.AddSeconds(-1)));

        Assert.True(pause.IsOpen);
    }
}
