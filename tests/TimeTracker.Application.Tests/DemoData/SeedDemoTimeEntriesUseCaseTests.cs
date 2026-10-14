using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.DemoData;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests.DemoData;

public class SeedDemoTimeEntriesUseCaseTests
{
    private static readonly Guid Luis = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Pablo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private static readonly DemoEmployee[] Employees =
    [
        new(Luis, DemoScheduleProfiles.EarlyBird),
        new(Pablo, DemoScheduleProfiles.HalfDay),
    ];

    private readonly InMemoryDemoStore _store = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Execute_FirstRun_CreatesTheWindowUpToYesterdayAndNothingForToday()
    {
        var result = await CreateUseCase().ExecuteAsync(Employees, CancellationToken.None);

        Assert.True(result.CreatedDays > 30, "Dos empleados con ~20 días laborables cada uno.");
        Assert.Equal(0, result.SkippedDays);
        Assert.Equal(_store.Added.Count, result.CreatedEntries);
        var days = _store.Added.Select(entry => SpainTime.DateOf(entry.ClockInAt)).Distinct().Order().ToList();
        Assert.InRange(days[0], new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18));
        Assert.InRange(days[^1], new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 13));
        Assert.DoesNotContain(days, day => day >= new DateOnly(2026, 10, 14));
        Assert.All(_store.Added, entry => Assert.False(entry.IsOpen));
        Assert.Equal(1, _store.AddRangeCalls);
    }

    [Fact]
    public async Task Execute_SameDayAgain_CreatesNothingAndCountsSkippedDays()
    {
        var first = await CreateUseCase().ExecuteAsync(Employees, CancellationToken.None);

        var second = await CreateUseCase().ExecuteAsync(Employees, CancellationToken.None);

        Assert.Equal(new DemoSeedResult(0, first.CreatedDays, 0), second);
        Assert.Equal(1, _store.AddRangeCalls);
        Assert.Equal(first.CreatedEntries, _store.Added.Count);
    }

    [Fact]
    public async Task Execute_TwoDaysLater_AddsOnlyTheNewDaysUpToYesterday()
    {
        await CreateUseCase().ExecuteAsync(Employees, CancellationToken.None);
        var before = _store.Added.Count;
        _clock.Advance(TimeSpan.FromDays(3)); // hoy pasa a ser el 17: faltan el 14, el 15 y el 16

        var result = await CreateUseCase().ExecuteAsync(Employees, CancellationToken.None);

        var newDays = _store.Added.Skip(before).Select(entry => SpainTime.DateOf(entry.ClockInAt)).Distinct().Order().ToList();
        Assert.NotEmpty(newDays);
        Assert.All(newDays, day => Assert.InRange(day, new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16)));
        Assert.Equal(_store.Added.Count - before, result.CreatedEntries);
        Assert.True(result.SkippedDays > 0);
    }

    [Fact]
    public async Task Execute_WindowSlidesWithoutChangingAlreadySeededDays()
    {
        var other = new InMemoryDemoStore();
        await CreateUseCase().ExecuteAsync(Employees, CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(2));
        await CreateUseCase().ExecuteAsync(Employees, CancellationToken.None);

        // Sembrar de golpe en la fecha final produce los mismos intervalos que sembrar en dos pasos.
        var direct = new SeedDemoTimeEntriesUseCase(other, new FakeTimeProvider(_clock.GetUtcNow()));
        await direct.ExecuteAsync(Employees, CancellationToken.None);

        Assert.Equal(Describe(other.Added), Describe(_store.Added.Where(entry => SpainTime.DateOf(entry.ClockInAt) >= new DateOnly(2026, 9, 16))));
    }

    [Fact]
    public async Task Execute_DayWithExistingEntry_IsSkippedAndTheRestAreCreated()
    {
        var someWeekday = new DateOnly(2026, 10, 7);
        var existing = TimeEntry.Start(Luis, TimeEntryType.Work, SpainTime.ToUtc(someWeekday, new TimeOnly(11, 0)));
        existing.Close(SpainTime.ToUtc(someWeekday, new TimeOnly(12, 0)));
        _store.Seed(existing);

        var result = await CreateUseCase().ExecuteAsync([Employees[0]], CancellationToken.None);

        Assert.Equal(1, result.SkippedDays);
        Assert.DoesNotContain(_store.Added, entry => SpainTime.DateOf(entry.ClockInAt) == someWeekday);
        Assert.Contains(_store.Added, entry => SpainTime.DateOf(entry.ClockInAt) == someWeekday.AddDays(1));
    }

    [Fact]
    public async Task Execute_ExistingEntryOfAnotherEmployee_DoesNotSkipDays()
    {
        var someWeekday = new DateOnly(2026, 10, 7);
        var existing = TimeEntry.Start(Pablo, TimeEntryType.Work, SpainTime.ToUtc(someWeekday, new TimeOnly(11, 0)));
        existing.Close(SpainTime.ToUtc(someWeekday, new TimeOnly(12, 0)));
        _store.Seed(existing);

        var result = await CreateUseCase().ExecuteAsync([Employees[0]], CancellationToken.None);

        Assert.Equal(0, result.SkippedDays);
    }

    [Fact]
    public async Task Execute_EntryAtLocalMidnightBoundary_CountsForItsLocalDay()
    {
        // 23:30 UTC del 6 de octubre son las 01:30 locales del 7: ese día local queda ocupado.
        var existing = TimeEntry.Start(Luis, TimeEntryType.Work, new DateTimeOffset(2026, 10, 6, 23, 30, 0, TimeSpan.Zero));
        existing.Close(new DateTimeOffset(2026, 10, 7, 0, 30, 0, TimeSpan.Zero));
        _store.Seed(existing);

        await CreateUseCase().ExecuteAsync([Employees[0]], CancellationToken.None);

        Assert.DoesNotContain(_store.Added, entry => SpainTime.DateOf(entry.ClockInAt) == new DateOnly(2026, 10, 7));
    }

    [Fact]
    public async Task Execute_WithoutEmployees_PersistsNothing()
    {
        var result = await CreateUseCase().ExecuteAsync([], CancellationToken.None);

        Assert.Equal(new DemoSeedResult(0, 0, 0), result);
        Assert.Equal(0, _store.AddRangeCalls);
    }

    private SeedDemoTimeEntriesUseCase CreateUseCase() => new(_store, _clock);

    private static List<(Guid, TimeEntryType, DateTimeOffset, DateTimeOffset?)> Describe(IEnumerable<TimeEntry> entries)
        => [.. entries
            .Select(entry => (entry.EmployeeId, entry.Type, entry.ClockInAt, entry.ClockOutAt))
            .OrderBy(item => item.EmployeeId)
            .ThenBy(item => item.ClockInAt)];

    private sealed class InMemoryDemoStore : IDemoTimeEntryStore
    {
        private readonly List<TimeEntry> _all = [];

        public List<TimeEntry> Added { get; } = [];

        public int AddRangeCalls { get; private set; }

        public void Seed(TimeEntry entry) => _all.Add(entry);

        public Task<IReadOnlyList<DateTimeOffset>> ListStartsBetweenAsync(Guid employeeId, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken)
        {
            IReadOnlyList<DateTimeOffset> starts = [.. _all
                .Where(entry => entry.EmployeeId == employeeId && entry.ClockInAt >= fromInclusive && entry.ClockInAt < toExclusive)
                .Select(entry => entry.ClockInAt)];
            return Task.FromResult(starts);
        }

        public Task AddRangeAsync(IReadOnlyCollection<TimeEntry> entries, CancellationToken cancellationToken)
        {
            AddRangeCalls++;
            _all.AddRange(entries);
            Added.AddRange(entries);
            return Task.CompletedTask;
        }
    }
}
