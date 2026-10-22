using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

[Collection("Postgres")]
public class DemoTimeEntryStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset From = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListStartsBetween_ReturnsOnlyTheEmployeesStartsInTheHalfOpenRange()
    {
        var employee = Guid.NewGuid();
        await AddRangeAsync(
            Closed(employee, From.AddMinutes(-1)),
            Closed(employee, From),
            Closed(employee, From.AddDays(10)),
            Closed(employee, To.AddMinutes(-1)),
            Closed(employee, To),
            Closed(Guid.NewGuid(), From.AddDays(10)));

        await using var context = postgres.CreateContext(postgres.ConnectionString);
        var starts = await new DemoTimeEntryStore(context).ListStartsBetweenAsync(employee, From, To, CancellationToken.None);

        Assert.Equal([From, From.AddDays(10), To.AddMinutes(-1)], starts.Order());
    }

    [Fact]
    public async Task AddRange_PersistsAllEntriesWithTheirTypesAndInstants()
    {
        var employee = Guid.NewGuid();
        var work = TimeEntry.Start(Company.DefaultId, employee, TimeEntryType.Work, From.AddHours(8));
        var meal = work.Pause(TimeEntryType.MealBreak, From.AddHours(12));
        var afternoon = meal.Resume(From.AddHours(13));
        afternoon.Close(From.AddHours(17));

        await AddRangeAsync(work, meal, afternoon);

        await using var context = postgres.CreateContext(postgres.ConnectionString);
        var stored = await context.TimeEntries.AsNoTracking().Where(entry => entry.EmployeeId == employee).OrderBy(entry => entry.ClockInAt).ToListAsync(CancellationToken.None);
        Assert.Equal([TimeEntryType.Work, TimeEntryType.MealBreak, TimeEntryType.Work], stored.Select(entry => entry.Type));
        Assert.All(stored, entry => Assert.False(entry.IsOpen));
        Assert.Equal(From.AddHours(17), stored[^1].ClockOutAt);
    }

    [Fact]
    public async Task AddRange_WhenOneEntryFails_PersistsNone()
    {
        var employee = Guid.NewGuid();
        var existing = Closed(employee, From.AddDays(1));
        await AddRangeAsync(existing);
        var fresh = Closed(employee, From.AddDays(2));

        // La segunda fila repite una clave primaria: la base de datos la rechaza y la primera no debe quedar guardada.
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        await Assert.ThrowsAsync<DbUpdateException>(() => new DemoTimeEntryStore(context).AddRangeAsync([fresh, existing], CancellationToken.None));

        await using var check = postgres.CreateContext(postgres.ConnectionString);
        Assert.Equal(1, await check.TimeEntries.CountAsync(entry => entry.EmployeeId == employee, CancellationToken.None));
    }

    private static TimeEntry Closed(Guid employeeId, DateTimeOffset start)
    {
        var entry = TimeEntry.Start(Company.DefaultId, employeeId, TimeEntryType.Work, start);
        entry.Close(start.AddHours(1));
        return entry;
    }

    private async Task AddRangeAsync(params TimeEntry[] entries)
    {
        foreach (var employee in entries.Select(entry => entry.EmployeeId).Distinct())
        {
            UserSeed.EnsureExists(postgres.ConnectionString, employee);
        }

        await using var context = postgres.CreateContext(postgres.ConnectionString);
        await new DemoTimeEntryStore(context).AddRangeAsync(entries, CancellationToken.None);
    }
}
