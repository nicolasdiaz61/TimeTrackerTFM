using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests;

public class ClockInUseCaseTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTimeEntryRepository _repository = new();
    private readonly FakeTimeProvider _time = new(EightAm);

    private ClockInUseCase CreateUseCase() => new(_repository, new FixedCurrentUser(EmployeeId), _time);

    [Fact]
    public async Task ClockIn_WithNoOpenEntry_CreatesEntryAtNow()
    {
        var result = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        var saved = Assert.Single(_repository.Added);
        Assert.Equal(EmployeeId, saved.EmployeeId);
        Assert.Equal(EightAm, saved.ClockInAt);
        Assert.True(saved.IsOpen);
        Assert.Equal(new TimeEntryResult(saved.Id, TimeEntryType.Work, EightAm, null, null), result);
    }

    [Fact]
    public async Task ClockIn_WithOpenEntry_ThrowsAndDoesNotSave()
    {
        _repository.Seed(TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm.AddHours(-1)));

        var exception = await Assert.ThrowsAsync<OpenTimeEntryAlreadyExistsException>(
            () => CreateUseCase().ExecuteAsync(CancellationToken.None));

        Assert.Equal("Ya existe un fichaje abierto", exception.Title);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task ClockIn_AfterClosingPrevious_CreatesNewEntry()
    {
        var previous = TimeEntry.Start(EmployeeId, TimeEntryType.Work, EightAm.AddDays(-1));
        previous.Close(EightAm.AddDays(-1).AddHours(8));
        _repository.Seed(previous);

        await CreateUseCase().ExecuteAsync(CancellationToken.None);

        var saved = Assert.Single(_repository.Added);
        Assert.True(saved.IsOpen);
        Assert.Equal(EightAm, saved.ClockInAt);
    }

    [Fact]
    public async Task ClockIn_WithOtherEmployeeOpenEntry_Succeeds()
    {
        var otherEmployee = Guid.Parse("9a7c1e52-0b3d-4f68-8a14-2e5d7b9c4f10");
        _repository.Seed(TimeEntry.Start(otherEmployee, TimeEntryType.Work, EightAm.AddHours(-1)));

        await CreateUseCase().ExecuteAsync(CancellationToken.None);

        var saved = Assert.Single(_repository.Added);
        Assert.Equal(EmployeeId, saved.EmployeeId);
    }

    [Theory]
    [InlineData(TimeEntryType.Break)]
    [InlineData(TimeEntryType.MealBreak)]
    public async Task ClockIn_WithOpenBreak_ThrowsAndDoesNotSave(TimeEntryType breakType)
    {
        _repository.Seed(TimeEntry.Start(EmployeeId, breakType, EightAm.AddHours(-1)));

        await Assert.ThrowsAsync<OpenTimeEntryAlreadyExistsException>(
            () => CreateUseCase().ExecuteAsync(CancellationToken.None));

        Assert.Empty(_repository.Added);
    }
}
