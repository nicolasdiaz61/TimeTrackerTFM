using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests;

public class ClockOutUseCaseTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTimeEntryRepository _repository = new();
    private readonly FakeTimeProvider _time = new(EightAm.AddHours(8));

    private ClockOutUseCase CreateUseCase() => new(_repository, new FixedCurrentUser(EmployeeId), _time);

    [Fact]
    public async Task ClockOut_WithOpenEntry_ClosesAtNowAndPersists()
    {
        var open = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        _repository.Seed(open);

        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(EightAm.AddHours(8), open.ClockOutAt);
        Assert.Same(open, Assert.Single(_repository.Updated));
        Assert.Equal(new TimeEntryResult(open.Id, TimeEntryType.Work, EightAm, EightAm.AddHours(8), 480), result);
    }

    [Fact]
    public async Task ClockOut_WithoutOpenEntry_Throws()
    {
        var exception = await Assert.ThrowsAsync<NoOpenTimeEntryException>(
            () => CreateUseCase().ExecuteAsync(CancellationToken.None));

        Assert.Equal("No hay ningún fichaje abierto", exception.Title);
        Assert.Empty(_repository.Updated);
    }

    [Fact]
    public async Task ClockOut_WhenClockBeforeClockIn_ThrowsAndEntryStaysOpen()
    {
        var open = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        _repository.Seed(open);
        var earlyClock = new FakeTimeProvider(EightAm.AddHours(-1));
        var useCase = new ClockOutUseCase(_repository, new FixedCurrentUser(EmployeeId), earlyClock);

        await Assert.ThrowsAsync<ClockOutBeforeClockInException>(
            () => useCase.ExecuteAsync(CancellationToken.None));

        Assert.True(open.IsOpen);
        Assert.Empty(_repository.Updated);
    }

    [Fact]
    public async Task ClockOut_DoesNotCloseOtherEmployeeEntry()
    {
        var otherEmployee = Guid.Parse("9a7c1e52-0b3d-4f68-8a14-2e5d7b9c4f10");
        var others = TimeEntry.Start(otherEmployee, TimeEntryType.Work, EightAm);
        var mine = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        _repository.Seed(others);
        _repository.Seed(mine);

        await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.True(others.IsOpen);
        Assert.False(mine.IsOpen);
    }

    [Fact]
    public async Task ClockOut_WithOnlyOtherEmployeeOpenEntry_Throws()
    {
        var others = TimeEntry.Start(Guid.Parse("9a7c1e52-0b3d-4f68-8a14-2e5d7b9c4f10"), TimeEntryType.Work, EightAm);
        _repository.Seed(others);

        await Assert.ThrowsAsync<NoOpenTimeEntryException>(
            () => CreateUseCase().ExecuteAsync(CancellationToken.None));

        Assert.True(others.IsOpen);
    }

    [Theory]
    [InlineData(TimeEntryType.Break)]
    [InlineData(TimeEntryType.MealBreak)]
    public async Task ClockOut_DuringBreak_ClosesItAndOpensNothing(TimeEntryType breakType)
    {
        var pause = TimeEntry.Start(EmployeeId, breakType, EightAm.AddHours(5));
        _repository.Seed(pause);

        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.False(pause.IsOpen);
        Assert.Equal(breakType, result.Type);
        Assert.Equal(180, result.DurationMinutes);
        Assert.Same(pause, Assert.Single(_repository.Updated));
        Assert.Empty(_repository.Added);
        Assert.Empty(_repository.Replaced);
    }
}
