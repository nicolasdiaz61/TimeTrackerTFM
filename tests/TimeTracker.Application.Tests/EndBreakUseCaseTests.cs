using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests;

public class EndBreakUseCaseTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset TenAm = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTimeEntryRepository _repository = new();
    private readonly FakeTimeProvider _time = new(TenAm.AddMinutes(15));

    private EndBreakUseCase CreateUseCase() => new(_repository, new FixedCurrentUser(EmployeeId), _time);

    [Theory]
    [InlineData(TimeEntryType.Break)]
    [InlineData(TimeEntryType.MealBreak)]
    public async Task EndBreak_OnBreak_ClosesItAndOpensWorkAtomically(TimeEntryType breakType)
    {
        var pause = TimeEntry.Start(EmployeeId, breakType, TenAm);
        _repository.Seed(pause);

        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        var (closed, next) = Assert.Single(_repository.Replaced);
        Assert.Same(pause, closed);
        Assert.Equal(TenAm.AddMinutes(15), pause.ClockOutAt);
        Assert.Equal(TimeEntryType.Work, next.Type);
        Assert.Equal(TenAm.AddMinutes(15), next.ClockInAt);
        Assert.Equal(new TimeEntryResult(next.Id, TimeEntryType.Work, TenAm.AddMinutes(15), null, null), result);
    }

    [Fact]
    public async Task EndBreak_WhileWorking_ThrowsNoOpenBreakAndPersistsNothing()
    {
        var work = TimeEntry.Start(EmployeeId, TimeEntryType.Work, TenAm);
        _repository.Seed(work);

        await Assert.ThrowsAsync<NoOpenBreakException>(() => CreateUseCase().ExecuteAsync(CancellationToken.None));

        Assert.True(work.IsOpen);
        Assert.Empty(_repository.Replaced);
    }

    [Fact]
    public async Task EndBreak_WithoutOpenEntry_ThrowsNoOpenBreak()
    {
        await Assert.ThrowsAsync<NoOpenBreakException>(() => CreateUseCase().ExecuteAsync(CancellationToken.None));

        Assert.Empty(_repository.Replaced);
    }

    [Fact]
    public async Task EndBreak_WithOnlyOtherEmployeeOnBreak_ThrowsAndLeavesItUntouched()
    {
        var others = TimeEntry.Start(Guid.Parse("9a7c1e52-0b3d-4f68-8a14-2e5d7b9c4f10"), TimeEntryType.Break, TenAm);
        _repository.Seed(others);

        await Assert.ThrowsAsync<NoOpenBreakException>(() => CreateUseCase().ExecuteAsync(CancellationToken.None));

        Assert.True(others.IsOpen);
    }
}
