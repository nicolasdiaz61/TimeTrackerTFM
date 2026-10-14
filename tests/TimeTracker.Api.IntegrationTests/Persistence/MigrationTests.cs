using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.Persistence;

[Collection("Postgres")]
public class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrate_OnEmptyDatabase_CreatesTableIndexesAndCheck()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        var migrations = await QueryAsync(connectionString, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\"");
        var tables = await QueryAsync(connectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");
        var indexes = await QueryAsync(connectionString, "SELECT indexname FROM pg_indexes WHERE tablename = 'time_entries'");
        var checks = await QueryAsync(connectionString, "SELECT conname FROM pg_constraint WHERE contype = 'c'");

        Assert.Contains(migrations, migration => migration.EndsWith("_InitialTimeEntries", StringComparison.Ordinal));
        Assert.Contains("time_entries", tables);
        Assert.Contains("ix_time_entries_employee_id_clock_in_at", indexes);
        Assert.Contains("ux_time_entries_employee_id_open", indexes);
        Assert.Contains("ck_time_entries_clock_out_after_clock_in", checks);
    }

    [Fact]
    public async Task Migrate_OnEmptyDatabase_AppliesInitialAndAddUsersInOrder()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        var migrations = await QueryAsync(connectionString, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
        var tables = await QueryAsync(connectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

        Assert.Equal(2, migrations.Count);
        Assert.Contains(migrations, migration => migration.EndsWith("_InitialTimeEntries", StringComparison.Ordinal));
        Assert.EndsWith("_AddUsers", migrations[1], StringComparison.Ordinal);
        Assert.Contains("time_entries", tables);
        Assert.Contains("users", tables);
    }

    [Fact]
    public async Task AddUsers_OnDatabaseWithEntries_KeepsThem()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        var employeeId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        await using (var context = postgres.CreateContext(connectionString))
        {
            var migrator = context.GetService<IMigrator>();
            var initial = context.GetService<IMigrationsAssembly>().Migrations.Keys.Single(id => id.EndsWith("_InitialTimeEntries", StringComparison.Ordinal));
            await migrator.MigrateAsync(initial, CancellationToken.None);
            await ExecuteAsync(
                connectionString,
                $"INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at) VALUES ('{entryId}', '{employeeId}', '2026-10-12T08:00:00Z', '2026-10-12T16:00:00Z')");

            await context.Database.MigrateAsync(CancellationToken.None);
        }

        var rows = await QueryAsync(
            connectionString,
            $"SELECT id || '|' || employee_id || '|' || to_char(clock_in_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') || '|' || to_char(clock_out_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') FROM time_entries");

        Assert.Equal([$"{entryId}|{employeeId}|2026-10-12T08:00:00|2026-10-12T16:00:00"], rows);
    }

    [Fact]
    public async Task Model_HasNoPendingChanges()
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Columns_HaveExpectedTypesAndNullability()
    {
        var columns = await QueryAsync(
            postgres.ConnectionString,
            "SELECT column_name || ':' || data_type || ':' || is_nullable FROM information_schema.columns WHERE table_name = 'time_entries'");

        Assert.Contains("id:uuid:NO", columns);
        Assert.Contains("employee_id:uuid:NO", columns);
        Assert.Contains("clock_in_at:timestamp with time zone:NO", columns);
        Assert.Contains("clock_out_at:timestamp with time zone:YES", columns);
    }

    [Fact]
    public async Task Columns_AreExactlyFour()
    {
        var columns = await QueryAsync(postgres.ConnectionString, "SELECT column_name FROM information_schema.columns WHERE table_name = 'time_entries'");

        // Sin columnas de concurrencia (xmin diferida, DD-9): el modelo no añade nada más.
        Assert.Equal(["clock_in_at", "clock_out_at", "employee_id", "id"], columns.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Indexes_HaveExpectedDefinitions()
    {
        var definitions = await QueryAsync(postgres.ConnectionString, "SELECT indexdef FROM pg_indexes WHERE tablename = 'time_entries'");

        Assert.Contains(definitions, definition => definition.Contains("ix_time_entries_employee_id_clock_in_at", StringComparison.Ordinal)
            && definition.Contains("clock_in_at DESC", StringComparison.Ordinal));
        Assert.Contains(definitions, definition => definition.Contains("ux_time_entries_employee_id_open", StringComparison.Ordinal)
            && definition.Contains("UNIQUE", StringComparison.Ordinal)
            && definition.Contains("WHERE (clock_out_at IS NULL)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Check_RejectsClockOutBeforeClockIn()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(Guid.NewGuid(), "2026-10-12T08:00:00Z", "2026-10-12T07:59:59Z"));

        Assert.Equal("ck_time_entries_clock_out_after_clock_in", error.ConstraintName);
    }

    [Fact]
    public async Task OpenIndex_RejectsSecondOpenRow()
    {
        var employeeId = Guid.NewGuid();
        await InsertAsync(employeeId, "2026-10-12T08:00:00Z", null);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(employeeId, "2026-10-12T09:00:00Z", null));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_time_entries_employee_id_open", error.ConstraintName);
    }

    [Fact]
    public async Task OpenIndex_AllowsOpenRowAfterClosedRow()
    {
        var employeeId = Guid.NewGuid();
        await InsertAsync(employeeId, "2026-10-12T08:00:00Z", "2026-10-12T16:00:00Z");

        await InsertAsync(employeeId, "2026-10-13T08:00:00Z", null);
    }

    private async Task InsertAsync(Guid employeeId, string clockInAt, string? clockOutAt)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            "INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at) VALUES (@id, @employee, @in, @out)",
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("employee", employeeId);
        command.Parameters.AddWithValue("in", DateTimeOffset.Parse(clockInAt, System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("out", clockOutAt is null ? DBNull.Value : DateTimeOffset.Parse(clockOutAt, System.Globalization.CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task<List<string>> QueryAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var rows = new List<string>();
        while (await reader.ReadAsync(CancellationToken.None))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
