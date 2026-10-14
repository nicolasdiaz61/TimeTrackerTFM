using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests;

public class StartBreakUseCaseTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTimeEntryRepository _repository = new();
    private readonly FakeTimeProvider _time = new(EightAm.AddHours(2));

    private StartBreakUseCase CreateUseCase() => new(_repository, new FixedCurrentUser(EmployeeId), _time);

    [Theory]
    [InlineData(TimeEntryType.Break)]
    [InlineData(TimeEntryType.MealBreak)]
    public async Task StartBreak_WhileWorking_ClosesWorkAndOpensBreakAtomically(TimeEntryType breakType)
    {
        var work = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        _repository.Seed(work);

        var result = await CreateUseCase().ExecuteAsync(breakType, CancellationToken.None);

        var (closed, next) = Assert.Single(_repository.Replaced);
        Assert.Same(work, closed);
        Assert.Equal(EightAm.AddHours(2), work.ClockOutAt);
        Assert.Equal(breakType, next.Type);
        Assert.Equal(EightAm.AddHours(2), next.ClockInAt);
        Assert.True(next.IsOpen);
        Assert.Equal(new TimeEntryResult(next.Id, breakType, EightAm.AddHours(2), null, null), result);
        Assert.Empty(_repository.Added);
        Assert.Empty(_repository.Updated);
    }

    [Fact]
    public async Task StartBreak_WithoutOpenEntry_ThrowsNotWorkingAndPersistsNothing()
    {
        await Assert.ThrowsAsync<NotWorkingException>(
            () => CreateUseCase().ExecuteAsync(TimeEntryType.Break, CancellationToken.None));

        Assert.Empty(_repository.Replaced);
    }

    [Fact]
    public async Task StartBreak_WhenAlreadyOnBreak_ThrowsAndKeepsBreakOpen()
    {
        var pause = TimeEntry.Start(EmployeeId, TimeEntryType.Break, EightAm);
        _repository.Seed(pause);

        await Assert.ThrowsAsync<NotWorkingException>(
            () => CreateUseCase().ExecuteAsync(TimeEntryType.MealBreak, CancellationToken.None));

        Assert.True(pause.IsOpen);
        Assert.Empty(_repository.Replaced);
    }

    [Fact]
    public async Task StartBreak_WithOnlyOtherEmployeeWorking_ThrowsAndLeavesItUntouched()
    {
        var others = TimeEntry.Start(Guid.Parse("9a7c1e52-0b3d-4f68-8a14-2e5d7b9c4f10"), TimeEntryType.Work, EightAm);
        _repository.Seed(others);

        await Assert.ThrowsAsync<NotWorkingException>(
            () => CreateUseCase().ExecuteAsync(TimeEntryType.Break, CancellationToken.None));

        Assert.True(others.IsOpen);
        Assert.Empty(_repository.Replaced);
    }

    [Fact]
    public async Task StartBreak_WhenClockBeforeStart_ThrowsAndWorkStaysOpen()
    {
        var work = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm);
        _repository.Seed(work);
        var early = new StartBreakUseCase(_repository, new FixedCurrentUser(EmployeeId), new FakeTimeProvider(EightAm.AddHours(-1)));

        await Assert.ThrowsAsync<ClockOutBeforeClockInException>(
            () => early.ExecuteAsync(TimeEntryType.Break, CancellationToken.None));

        Assert.True(work.IsOpen);
        Assert.Empty(_repository.Replaced);
    }

    [Fact]
    public async Task StartBreak_WithWorkAsBreakType_Throws()
    {
        _repository.Seed(TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateUseCase().ExecuteAsync(TimeEntryType.Work, CancellationToken.None));

        Assert.Empty(_repository.Replaced);
    }
}
