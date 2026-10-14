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
        var entry = TimeEntry.Start(EmployeeId, clockIn);
        entry.Close(clockIn.AddMinutes(minutes));
        _repository.Seed(entry);
        return entry;
    }

    [Fact]
    public async Task Get_WithClosedAndOpenEntries_TotalsOnlyClosed()
    {
        SeedClosed(EightAm, 60);
        SeedClosed(EightAm.AddHours(2), 30);
        var open = TimeEntry.Start(EmployeeId, EightAm.AddHours(4));
        _repository.Seed(open);

        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(90, result.TotalWorkedMinutes);
        Assert.Equal(3, result.Items.Count);
        Assert.Null(result.Items.Single(item => item.Id == open.Id).WorkedMinutes);
    }

    [Fact]
    public async Task Get_WithMoreThanFiftyEntries_ReturnsFiftyMostRecent()
    {
        for (var day = 0; day < 55; day++)
        {
            SeedClosed(EightAm.AddDays(day), 10);
        }

        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(50, result.Items.Count);
        Assert.Equal(EightAm.AddDays(54), result.Items[0].ClockInAt);
        Assert.Equal(EightAm.AddDays(5), result.Items[^1].ClockInAt);
    }

    [Fact]
    public async Task Get_OrdersByClockInDescending()
    {
        SeedClosed(EightAm.AddDays(-1), 10);
        SeedClosed(EightAm, 10);
        SeedClosed(EightAm.AddDays(-2), 10);

        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(
            [EightAm, EightAm.AddDays(-1), EightAm.AddDays(-2)],
            result.Items.Select(item => item.ClockInAt));
    }

    [Fact]
    public async Task Get_WithNoEntries_ReturnsEmptyAndZeroTotal()
    {
        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalWorkedMinutes);
    }

    [Fact]
    public async Task Get_TotalIsLimitedToTheFiftyReturned()
    {
        for (var day = 0; day < 55; day++)
        {
            SeedClosed(EightAm.AddDays(day), 10);
        }

        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(50, result.Items.Count);
        Assert.Equal(500, result.TotalWorkedMinutes);
    }
}
