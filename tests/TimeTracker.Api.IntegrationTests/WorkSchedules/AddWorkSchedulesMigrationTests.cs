using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Support.MigrationDatabase;

namespace TimeTracker.Api.IntegrationTests.WorkSchedules;

/// <summary>Migración aditiva de jornadas: no toca los datos existentes y las claves y restricciones nuevas se cumplen.</summary>
[Collection("Postgres")]
public class AddWorkSchedulesMigrationTests(PostgresFixture postgres)
{
    private const string AddWorkSchedules = "_AddWorkSchedules";

    private const string UserRows =
        "SELECT id || '|' || company_id || '|' || email || '|' || display_name || '|' || password_hash || '|' || role || '|' || COALESCE(manager_id::text, 'null') || '|' || must_change_password FROM users ORDER BY email";

    private static string Schedule(Guid id, string company, string name, string extra = "")
        => $"INSERT INTO work_schedules (id, company_id, name, start_time, lunch_start, lunch_minutes, expected_break_minutes, days_mask) VALUES ('{id}', '{company}', '{name}', '09:00', '14:00', 60, 30, 31{extra})";

    [Fact]
    public async Task Up_OverADatabaseWithData_KeepsEveryRowAndLeavesTheNewColumnNull()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await MigrateToAsync(context, EnforceCompanyIsolation);
        var before = await QueryAsync(database.ConnectionString, UserRows);
        var entriesBefore = await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM time_entries");

        await context.Database.MigrateAsync(CancellationToken.None);

        Assert.Equal(before, await QueryAsync(database.ConnectionString, UserRows));
        Assert.Equal(entriesBefore, await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM time_entries"));
        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM users WHERE work_schedule_id IS NOT NULL"));
        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM work_schedules"));
    }

    [Fact]
    public async Task Down_RemovesTheTableAndTheColumn_AndKeepsTheUsers()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        var before = await QueryAsync(database.ConnectionString, UserRows);

        await MigrateToAsync(context, EnforceCompanyIsolation);

        Assert.Equal(before, await QueryAsync(database.ConnectionString, UserRows));
        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM information_schema.tables WHERE table_name = 'work_schedules'"));
        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM information_schema.columns WHERE table_name = 'users' AND column_name = 'work_schedule_id'"));
    }

    [Fact]
    public async Task TheConstraints_RejectDuplicateNamesInACompany_ForeignCompanySchedules_AndOutOfRangeValues()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        var otherCompany = await TimeTracker.Api.IntegrationTests.Support.CompanySeed.InsertAsync(database.ConnectionString);
        var own = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        await ExecuteAsync(database.ConnectionString, Schedule(own, DefaultCompany, "Oficina"));
        await ExecuteAsync(database.ConnectionString, Schedule(foreign, otherCompany.ToString(), "Oficina"));

        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, Schedule(Guid.NewGuid(), DefaultCompany, "Oficina")));
        var crossCompany = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, $"UPDATE users SET work_schedule_id = '{foreign}' WHERE email = 'luis.despues@example.test'"));
        var badMinutes = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, Schedule(Guid.NewGuid(), DefaultCompany, "Mala").Replace("60, 30", "481, 30", StringComparison.Ordinal)));
        await ExecuteAsync(database.ConnectionString, $"UPDATE users SET work_schedule_id = '{own}' WHERE email = 'luis.despues@example.test'");
        var inUse = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, $"DELETE FROM work_schedules WHERE id = '{own}'"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, crossCompany.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, badMinutes.SqlState);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, inUse.SqlState);
    }
}
