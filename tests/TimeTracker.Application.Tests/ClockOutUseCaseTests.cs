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
        var open = TimeEntry.Start(EmployeeId, EightAm);
        _repository.Seed(open);

        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(EightAm.AddHours(8), open.ClockOutAt);
        Assert.Same(open, Assert.Single(_repository.Updated));
        Assert.Equal(new TimeEntryResult(open.Id, EightAm, EightAm.AddHours(8), 480), result);
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
        var open = TimeEntry.Start(EmployeeId, EightAm);
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
        var others = TimeEntry.Start(otherEmployee, EightAm);
        var mine = TimeEntry.Start(EmployeeId, EightAm);
        _repository.Seed(others);
        _repository.Seed(mine);

        await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.True(others.IsOpen);
        Assert.False(mine.IsOpen);
    }

    [Fact]
    public async Task ClockOut_WithOnlyOtherEmployeeOpenEntry_Throws()
    {
        var others = TimeEntry.Start(Guid.Parse("9a7c1e52-0b3d-4f68-8a14-2e5d7b9c4f10"), EightAm);
        _repository.Seed(others);

        await Assert.ThrowsAsync<NoOpenTimeEntryException>(
            () => CreateUseCase().ExecuteAsync(CancellationToken.None));

        Assert.True(others.IsOpen);
    }
}
