using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Support.MigrationDatabase;

namespace TimeTracker.Api.IntegrationTests.WorkSchedules;

/// <summary>Migración aditiva de la hora de salida: las jornadas existentes se conservan con la columna nula y <c>Down</c> la elimina.</summary>
[Collection("Postgres")]
public class AddWorkScheduleEndTimeMigrationTests(PostgresFixture postgres)
{
    private const string Rows = "SELECT name || '|' || start_time || '|' || COALESCE(end_time::text, 'null') FROM work_schedules ORDER BY name";

    private static string Schedule(string company)
        => $"INSERT INTO work_schedules (id, company_id, name, start_time, lunch_start, lunch_minutes, expected_break_minutes, days_mask) VALUES ('{Guid.NewGuid()}', '{company}', 'Oficina', '09:00', '14:00', 60, 30, 31)";

    [Fact]
    public async Task Up_KeepsExistingSchedules_WithANullEndTime()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await MigrateToAsync(context, AddGroqProvider);
        await ExecuteAsync(database.ConnectionString, Schedule(DefaultCompany));

        await context.Database.MigrateAsync(CancellationToken.None);

        Assert.Equal(["Oficina|09:00:00|null"], await QueryAsync(database.ConnectionString, Rows));
        await ExecuteAsync(database.ConnectionString, "UPDATE work_schedules SET end_time = '18:00'");
        Assert.Equal(["Oficina|09:00:00|18:00:00"], await QueryAsync(database.ConnectionString, Rows));
    }

    [Fact]
    public async Task Down_DropsTheColumn_AndKeepsTheSchedules()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        await ExecuteAsync(database.ConnectionString, Schedule(DefaultCompany));

        await MigrateToAsync(context, AddGroqProvider);

        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM information_schema.columns WHERE table_name = 'work_schedules' AND column_name = 'end_time'"));
        Assert.Equal(["1"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM work_schedules"));
    }
}
