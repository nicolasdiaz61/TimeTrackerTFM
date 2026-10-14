using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

[Collection("Postgres")]
public class TimeEntryRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsync_ThenReadWithNewContext_KeepsUtcInstant()
    {
        var entry = TimeEntry.Start(Guid.NewGuid(), TimeEntryType.Work, EightAm);
        await AddAsync(entry);

        var read = await FindOpenAsync(entry.EmployeeId);

        Assert.NotNull(read);
        Assert.Equal(entry.Id, read.Id);
        Assert.Equal(EightAm, read.ClockInAt);
        Assert.Equal(TimeSpan.Zero, read.ClockInAt.Offset);
        Assert.Null(read.ClockOutAt);
    }

    [Fact]
    public async Task FindOpenAsync_IgnoresOtherEmployeesOpenEntry()
    {
        await AddAsync(TimeEntry.Start(Guid.NewGuid(), TimeEntryType.Work, EightAm));

        Assert.Null(await FindOpenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListLatestAsync_ReturnsFiftyNewestOfThatEmployeeOnly()
    {
        var employeeId = Guid.NewGuid();
        for (var index = 0; index < 55; index++)
        {
            await AddAsync(ClosedEntry(employeeId, EightAm.AddHours(index)));
        }

        for (var index = 0; index < 3; index++)
        {
            await AddAsync(ClosedEntry(Guid.NewGuid(), EightAm.AddHours(100 + index)));
        }

        await using var context = postgres.CreateContext(postgres.ConnectionString);
        var latest = await new TimeEntryRepository(context).ListLatestAsync(employeeId, 50, CancellationToken.None);

        Assert.Equal(50, latest.Count);
        Assert.All(latest, entry => Assert.Equal(employeeId, entry.EmployeeId));
        Assert.Equal(EightAm.AddHours(54), latest[0].ClockInAt);
        Assert.Equal(EightAm.AddHours(5), latest[^1].ClockInAt);
        Assert.Equal(latest.OrderByDescending(entry => entry.ClockInAt), latest);
    }

    [Fact]
    public async Task AddAsync_SecondOpenEntry_ThrowsOpenTimeEntryAlreadyExists()
    {
        var employeeId = Guid.NewGuid();
        await AddAsync(TimeEntry.Start(employeeId, TimeEntryType.Work, EightAm));

        await Assert.ThrowsAsync<OpenTimeEntryAlreadyExistsException>(() => AddAsync(TimeEntry.Start(employeeId, TimeEntryType.Work, EightAm.AddHours(1))));
    }

    [Fact]
    public async Task AddAsync_OtherUniqueViolation_IsNotTranslated()
    {
        var closed = ClosedEntry(Guid.NewGuid(), EightAm);
        await AddAsync(closed);
        await using var reader = postgres.CreateContext(postgres.ConnectionString);
        var sameId = (await new TimeEntryRepository(reader).ListLatestAsync(closed.EmployeeId, 1, CancellationToken.None))[0];

        // Misma clave primaria, fila cerrada: viola una restricción única distinta de la de entrada abierta.
        await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(sameId));
    }

    [Fact]
    public async Task AddAsync_OpenEntriesOfDifferentEmployees_AreAllowed()
    {
        await AddAsync(TimeEntry.Start(Guid.NewGuid(), TimeEntryType.Work, EightAm));

        await AddAsync(TimeEntry.Start(Guid.NewGuid(), TimeEntryType.Work, EightAm));
    }

    [Fact]
    public async Task UpdateAsync_PersistsClosedEntry()
    {
        var employeeId = Guid.NewGuid();
        await AddAsync(TimeEntry.Start(employeeId, TimeEntryType.Work, EightAm));

        await using (var context = postgres.CreateContext(postgres.ConnectionString))
        {
            var repository = new TimeEntryRepository(context);
            var open = await repository.FindOpenAsync(employeeId, CancellationToken.None);
            open!.Close(EightAm.AddHours(8));
            await repository.UpdateAsync(open, CancellationToken.None);
        }

        await using var reader = postgres.CreateContext(postgres.ConnectionString);
        var stored = (await new TimeEntryRepository(reader).ListLatestAsync(employeeId, 1, CancellationToken.None))[0];
        Assert.Equal(EightAm.AddHours(8), stored.ClockOutAt);
        Assert.Null(await FindOpenAsync(employeeId));
    }

    [Fact]
    public async Task AddAsync_TenParallelOpenInserts_OneSucceedsNineConflict()
    {
        var employeeId = Guid.NewGuid();

        var attempts = Enumerable.Range(0, 10).Select(async index =>
        {
            try
            {
                await AddAsync(TimeEntry.Start(employeeId, TimeEntryType.Work, EightAm.AddSeconds(index)));
                return true;
            }
            catch (OpenTimeEntryAlreadyExistsException)
            {
                return false;
            }
        });
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, succeeded => succeeded);
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        var stored = await new TimeEntryRepository(context).ListLatestAsync(employeeId, 50, CancellationToken.None);
        Assert.Single(stored);
        Assert.Null((await FindOpenAsync(employeeId))!.ClockOutAt);
    }

    [Theory]
    [InlineData(TimeEntryType.Work, "Work")]
    [InlineData(TimeEntryType.Break, "Break")]
    [InlineData(TimeEntryType.MealBreak, "MealBreak")]
    public async Task AddAsync_StoresTypeAsReadableText_AndReadsItBack(TimeEntryType type, string stored)
    {
        var entry = TimeEntry.Start(Guid.NewGuid(), type, EightAm);
        await AddAsync(entry);

        var read = await FindOpenAsync(entry.EmployeeId);
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand($"SELECT type FROM time_entries WHERE id = '{entry.Id}'", connection);

        Assert.Equal(type, read!.Type);
        Assert.Equal(stored, (string?)await command.ExecuteScalarAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReplaceOpenAsync_ClosesWorkAndOpensBreakTogether()
    {
        var employeeId = Guid.NewGuid();
        await AddAsync(TimeEntry.Start(employeeId, TimeEntryType.Work, EightAm));

        await using (var context = postgres.CreateContext(postgres.ConnectionString))
        {
            var repository = new TimeEntryRepository(context);
            var work = (await repository.FindOpenAsync(employeeId, CancellationToken.None))!;
            var pause = work.Pause(TimeEntryType.Break, EightAm.AddHours(2));
            await repository.ReplaceOpenAsync(work, pause, CancellationToken.None);
        }

        await using var reader = postgres.CreateContext(postgres.ConnectionString);
        var stored = await new TimeEntryRepository(reader).ListLatestAsync(employeeId, 10, CancellationToken.None);
        Assert.Equal([TimeEntryType.Break, TimeEntryType.Work], stored.Select(entry => entry.Type));
        Assert.Equal(EightAm.AddHours(2), stored[1].ClockOutAt);
        Assert.True(stored[0].IsOpen);
    }

    [Fact]
    public async Task ReplaceOpenAsync_WhenTheInsertFailsAfterTheClose_RollsBackTheClose()
    {
        var employeeId = Guid.NewGuid();
        var rivalId = Guid.NewGuid();
        await AddAsync(TimeEntry.Start(employeeId, TimeEntryType.Work, EightAm));
        await AddAsync(TimeEntry.Start(rivalId, TimeEntryType.Work, EightAm));

        await using (var context = postgres.CreateContext(postgres.ConnectionString))
        {
            var repository = new TimeEntryRepository(context);
            var work = (await repository.FindOpenAsync(employeeId, CancellationToken.None))!;
            work.Close(EightAm.AddHours(1));
            // El nuevo intervalo pertenece a otro empleado que ya tiene uno abierto: el índice único rechaza la inserción
            // después de que el cierre se haya aplicado dentro de la transacción.
            var conflicting = TimeEntry.Start(rivalId, TimeEntryType.Break, EightAm.AddHours(1));

            await Assert.ThrowsAsync<ConcurrentTimeEntryChangeException>(
                () => repository.ReplaceOpenAsync(work, conflicting, CancellationToken.None));
        }

        var stillOpen = await FindOpenAsync(employeeId);
        Assert.NotNull(stillOpen);
        Assert.Equal(TimeEntryType.Work, stillOpen.Type);
    }

    [Fact]
    public async Task ReplaceOpenAsync_WhenAnotherContextClosedTheEntry_ThrowsConcurrentChangeAndInsertsNothing()
    {
        var employeeId = Guid.NewGuid();
        await AddAsync(TimeEntry.Start(employeeId, TimeEntryType.Work, EightAm));
        await using var first = postgres.CreateContext(postgres.ConnectionString);
        await using var second = postgres.CreateContext(postgres.ConnectionString);
        var firstRepository = new TimeEntryRepository(first);
        var secondRepository = new TimeEntryRepository(second);
        var firstCopy = (await firstRepository.FindOpenAsync(employeeId, CancellationToken.None))!;
        var secondCopy = (await secondRepository.FindOpenAsync(employeeId, CancellationToken.None))!;
        firstCopy.Close(EightAm.AddHours(1));
        await firstRepository.UpdateAsync(firstCopy, CancellationToken.None);

        var pause = secondCopy.Pause(TimeEntryType.Break, EightAm.AddHours(2));
        await Assert.ThrowsAsync<ConcurrentTimeEntryChangeException>(
            () => secondRepository.ReplaceOpenAsync(secondCopy, pause, CancellationToken.None));

        Assert.Null(await FindOpenAsync(employeeId));
    }

    [Fact]
    public async Task UpdateAsync_WhenAnotherContextChangedTheEntry_ThrowsConcurrentChange()
    {
        var employeeId = Guid.NewGuid();
        await AddAsync(TimeEntry.Start(employeeId, TimeEntryType.Work, EightAm));
        await using var first = postgres.CreateContext(postgres.ConnectionString);
        await using var second = postgres.CreateContext(postgres.ConnectionString);
        var firstRepository = new TimeEntryRepository(first);
        var secondRepository = new TimeEntryRepository(second);
        var firstCopy = (await firstRepository.FindOpenAsync(employeeId, CancellationToken.None))!;
        var secondCopy = (await secondRepository.FindOpenAsync(employeeId, CancellationToken.None))!;
        firstCopy.Close(EightAm.AddHours(1));
        await firstRepository.UpdateAsync(firstCopy, CancellationToken.None);
        secondCopy.Close(EightAm.AddHours(2));

        await Assert.ThrowsAsync<ConcurrentTimeEntryChangeException>(
            () => secondRepository.UpdateAsync(secondCopy, CancellationToken.None));
    }

    private static TimeEntry ClosedEntry(Guid employeeId, DateTimeOffset clockInAt)
    {
        var entry = TimeEntry.Start(employeeId, TimeEntryType.Work, clockInAt);
        entry.Close(clockInAt.AddMinutes(30));
        return entry;
    }

    private async Task AddAsync(TimeEntry entry)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        await new TimeEntryRepository(context).AddAsync(entry, CancellationToken.None);
    }

    private async Task<TimeEntry?> FindOpenAsync(Guid employeeId)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await new TimeEntryRepository(context).FindOpenAsync(employeeId, CancellationToken.None);
    }
}
