using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests;

public class GetTimeEntriesUseCaseTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTimeEntryRepository _repository = new();

    private GetTimeEntriesUseCase CreateUseCase() => new(_repository, new FixedCurrentUser(EmployeeId));

    private TimeEntry SeedClosed(DateTimeOffset clockIn, int minutes)
    {
        var entry = TimeEntry.Start(EmployeeId, TimeEntryType.Work, clockIn);
        entry.Close(clockIn.AddMinutes(minutes));
        _repository.Seed(entry);
        return entry;
    }

    [Fact]
    public async Task Get_WithClosedAndOpenEntries_TotalsOnlyClosed()
    {
        SeedClosed(EightAm, 60);
        SeedClosed(EightAm.AddHours(2), 30);
        var open = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm.AddHours(4));
        _repository.Seed(open);

        var result = await CreateUseCase().ExecuteAsync(null, CancellationToken.None);

        Assert.Equal(90, result.TotalWorkedMinutes);
        Assert.Equal(3, result.Items.Count);
        Assert.Null(result.Items.Single(item => item.Id == open.Id).DurationMinutes);
    }

    [Fact]
    public void MaxEntries_CoversAMonthOfBreaks() => Assert.Equal(300, GetTimeEntriesUseCase.MaxEntries);

    [Fact]
    public async Task Get_WithMoreThanTheLimit_ReturnsTheMostRecent()
    {
        for (var day = 0; day < GetTimeEntriesUseCase.MaxEntries + 5; day++)
        {
            SeedClosed(EightAm.AddDays(day), 10);
        }

        var result = await CreateUseCase().ExecuteAsync(null, CancellationToken.None);

        Assert.Equal(GetTimeEntriesUseCase.MaxEntries, result.Items.Count);
        Assert.Equal(EightAm.AddDays(GetTimeEntriesUseCase.MaxEntries + 4), result.Items[0].ClockInAt);
        Assert.Equal(EightAm.AddDays(5), result.Items[^1].ClockInAt);
    }

    [Fact]
    public async Task Get_OrdersByClockInDescending()
    {
        SeedClosed(EightAm.AddDays(-1), 10);
        SeedClosed(EightAm, 10);
        SeedClosed(EightAm.AddDays(-2), 10);

        var result = await CreateUseCase().ExecuteAsync(null, CancellationToken.None);

        Assert.Equal(
            [EightAm, EightAm.AddDays(-1), EightAm.AddDays(-2)],
            result.Items.Select(item => item.ClockInAt));
    }

    [Fact]
    public async Task Get_WithNoEntries_ReturnsEmptyAndZeroTotal()
    {
        var result = await CreateUseCase().ExecuteAsync(null, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalWorkedMinutes);
    }

    [Fact]
    public async Task Get_TotalIsLimitedToTheReturned()
    {
        for (var day = 0; day < GetTimeEntriesUseCase.MaxEntries + 5; day++)
        {
            SeedClosed(EightAm.AddDays(day), 10);
        }

        var result = await CreateUseCase().ExecuteAsync(null, CancellationToken.None);

        Assert.Equal(GetTimeEntriesUseCase.MaxEntries, result.Items.Count);
        Assert.Equal(GetTimeEntriesUseCase.MaxEntries * 10, result.TotalWorkedMinutes);
    }

    private static TimeWindow WindowOf(DateTimeOffset from, DateTimeOffset to)
    {
        Assert.True(TimeWindow.TryCreate(from, to, to, out var window, out _));
        return window!;
    }

    [Fact]
    public void MaxWindowEntries_BoundsAWindowedList() => Assert.Equal(1000, GetTimeEntriesUseCase.MaxWindowEntries);

    [Fact]
    public async Task Get_WithAWindow_ReturnsOnlyEntriesOverlappingIt()
    {
        var from = EightAm;
        var to = EightAm.AddDays(1);
        SeedClosed(from.AddHours(-2), 120);          // acaba justo en from: fuera
        var crossingStart = SeedClosed(from.AddHours(-1), 120);   // empieza antes y acaba dentro
        var inside = SeedClosed(from.AddHours(3), 30);
        var crossingEnd = SeedClosed(to.AddHours(-1), 120);       // empieza dentro y acaba después
        SeedClosed(to, 30);                           // empieza justo en to: fuera

        var result = await CreateUseCase().ExecuteAsync(WindowOf(from, to), CancellationToken.None);

        Assert.Equal([crossingEnd.Id, inside.Id, crossingStart.Id], result.Items.Select(item => item.Id));
        Assert.Equal(GetTimeEntriesUseCase.MaxWindowEntries, _repository.LastRequestedLimit);
        Assert.Equal(0, _repository.ListLatestCalls);
    }

    [Fact]
    public async Task Get_WithAWindow_StatusComesFromTheOpenEntryOutsideIt()
    {
        SeedClosed(EightAm.AddDays(-60), 30);
        _repository.Seed(TimeEntry.Start(EmployeeId, TimeEntryType.Break, EightAm));

        var result = await CreateUseCase().ExecuteAsync(WindowOf(EightAm.AddDays(-61), EightAm.AddDays(-59)), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(TimeTrackingStatus.OnBreak, result.Status);
    }

    [Fact]
    public async Task Get_WithAWindow_IncludesAnOpenEntryStartedBeforeItsEnd()
    {
        var open = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        _repository.Seed(open);

        var result = await CreateUseCase().ExecuteAsync(WindowOf(EightAm.AddDays(1), EightAm.AddDays(2)), CancellationToken.None);

        Assert.Equal([open.Id], result.Items.Select(item => item.Id));
        Assert.Equal(TimeTrackingStatus.Working, result.Status);
    }

    [Fact]
    public async Task Get_WithAWindow_TotalsOnlyTheReturnedClosedEntries()
    {
        SeedClosed(EightAm.AddDays(-5), 100);
        SeedClosed(EightAm, 40);

        var result = await CreateUseCase().ExecuteAsync(WindowOf(EightAm.AddHours(-1), EightAm.AddHours(1)), CancellationToken.None);

        Assert.Equal(40, result.TotalWorkedMinutes);
    }
}
