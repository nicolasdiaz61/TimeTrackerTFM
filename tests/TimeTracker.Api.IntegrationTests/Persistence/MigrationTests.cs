using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;

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
    public async Task Migrate_OnEmptyDatabase_AppliesMigrationsInOrder()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        var migrations = await QueryAsync(connectionString, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
        var tables = await QueryAsync(connectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

        Assert.Equal(10, migrations.Count);
        Assert.Contains(migrations, migration => migration.EndsWith("_InitialTimeEntries", StringComparison.Ordinal));
        Assert.EndsWith("_AddUsers", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("_AddTimeEntryType", migrations[2], StringComparison.Ordinal);
        Assert.EndsWith("_AddAssistantSettings", migrations[3], StringComparison.Ordinal);
        Assert.EndsWith("_AddAssistantAuditImmutability", migrations[4], StringComparison.Ordinal);
        Assert.EndsWith("_AddCompanies", migrations[5], StringComparison.Ordinal);
        Assert.EndsWith("_EnforceCompanyIsolation", migrations[6], StringComparison.Ordinal);
        Assert.EndsWith("_AddWorkSchedules", migrations[7], StringComparison.Ordinal);
        Assert.EndsWith("_AddGroqProvider", migrations[8], StringComparison.Ordinal);
        Assert.EndsWith("_AddWorkScheduleEndTime", migrations[9], StringComparison.Ordinal);
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

            await MigrateToAsync(context, "_AddUsers");
        }

        var rows = await QueryAsync(
            connectionString,
            $"SELECT id || '|' || employee_id || '|' || to_char(clock_in_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') || '|' || to_char(clock_out_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') FROM time_entries");

        Assert.Equal([$"{entryId}|{employeeId}|2026-10-12T08:00:00|2026-10-12T16:00:00"], rows);
    }

    [Fact]
    public async Task AddTimeEntryType_OnDatabaseWithOldEntries_KeepsThemAsWork()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        var employeeId = Guid.NewGuid();
        var closedId = Guid.NewGuid();
        var openId = Guid.NewGuid();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await MigrateToAsync(context, "_AddUsers");
            await ExecuteAsync(
                connectionString,
                $"INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at) VALUES ('{closedId}', '{employeeId}', '2026-10-12T08:00:00Z', '2026-10-12T16:00:00Z'), ('{openId}', '{employeeId}', '2026-10-13T08:00:00Z', NULL)");

            await MigrateToAsync(context, "_AddTimeEntryType");
        }

        var rows = await QueryAsync(
            connectionString,
            "SELECT id || '|' || employee_id || '|' || to_char(clock_in_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') || '|' || COALESCE(to_char(clock_out_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS'), 'null') || '|' || type FROM time_entries ORDER BY clock_in_at");

        Assert.Equal(
            [
                $"{closedId}|{employeeId}|2026-10-12T08:00:00|2026-10-12T16:00:00|Work",
                $"{openId}|{employeeId}|2026-10-13T08:00:00|null|Work",
            ],
            rows);
    }

    [Fact]
    public async Task AddTimeEntryType_Down_RemovesColumnAndConstraintKeepingRows()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        var entryId = Guid.NewGuid();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await MigrateToAsync(context, "_AddTimeEntryType");
            await ExecuteAsync(
                connectionString,
                $"INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at, type) VALUES ('{entryId}', '{Guid.NewGuid()}', '2026-10-12T08:00:00Z', '2026-10-12T09:00:00Z', 'Break')");

            await MigrateToAsync(context, "_AddUsers");
        }

        var columns = await QueryAsync(connectionString, "SELECT column_name FROM information_schema.columns WHERE table_name = 'time_entries'");
        var checks = await QueryAsync(connectionString, "SELECT conname FROM pg_constraint WHERE contype = 'c'");
        var ids = await QueryAsync(connectionString, "SELECT id::text FROM time_entries");

        Assert.DoesNotContain("type", columns);
        Assert.DoesNotContain("ck_time_entries_type", checks);
        Assert.Equal([entryId.ToString()], ids);
    }

    [Fact]
    public async Task AddAssistantSettings_OnDatabaseWithData_OnlyAddsTheTwoTablesAndKeepsTheRows()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        var entryId = Guid.NewGuid();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await MigrateToAsync(context, "_AddTimeEntryType");
            await ExecuteAsync(
                connectionString,
                $"INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at, type) VALUES ('{entryId}', '{Guid.NewGuid()}', '2026-10-12T08:00:00Z', '2026-10-12T09:00:00Z', 'Break')");
            var before = await TableShapeAsync(connectionString);

            await MigrateToAsync(context, "_AddAssistantSettings");

            var after = await TableShapeAsync(connectionString);
            Assert.Equal(before, after.Where(line => !line.StartsWith("assistant_settings", StringComparison.Ordinal)));
            Assert.Contains(after, line => line.StartsWith("assistant_settings:", StringComparison.Ordinal));
            Assert.Contains(after, line => line.StartsWith("assistant_settings_audit:", StringComparison.Ordinal));
        }

        Assert.Equal([entryId.ToString()], await QueryAsync(connectionString, "SELECT id::text FROM time_entries"));
    }

    [Fact]
    public async Task AddAssistantSettings_Down_DropsOnlyTheTwoNewTables()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var context = postgres.CreateContext(connectionString);
        await MigrateToAsync(context, "_AddTimeEntryType");
        var before = await TableShapeAsync(connectionString);
        await context.Database.MigrateAsync(CancellationToken.None);

        await MigrateToAsync(context, "_AddTimeEntryType");

        Assert.Equal(before, await TableShapeAsync(connectionString));
        Assert.DoesNotContain("assistant_settings", await QueryAsync(connectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"));
        Assert.DoesNotContain("assistant_settings_audit", await QueryAsync(connectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"));
    }

    [Fact]
    public async Task AddAssistantAuditImmutability_OnDatabaseWithData_ChangesNoTableAndKeepsTheRows()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        var entryId = Guid.NewGuid();
        await using var context = postgres.CreateContext(connectionString);
        await MigrateToAsync(context, "_AddAssistantSettings");
        await ExecuteAsync(
            connectionString,
            $"INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at, type) VALUES ('{entryId}', '{Guid.NewGuid()}', '2026-10-12T08:00:00Z', '2026-10-12T09:00:00Z', 'Break')");
        await ExecuteAsync(connectionString, "INSERT INTO assistant_settings_audit (occurred_at, actor_id, action, credential_changed) VALUES (now(), gen_random_uuid(), 'Saved', false)");
        var before = await TableShapeAsync(connectionString);

        await MigrateToAsync(context, "_AddAssistantAuditImmutability");

        Assert.Equal(before, await TableShapeAsync(connectionString));
        Assert.Equal([entryId.ToString()], await QueryAsync(connectionString, "SELECT id::text FROM time_entries"));
        Assert.Equal(["1"], await QueryAsync(connectionString, "SELECT count(*)::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task AssistantSettingsAudit_HasADescendingIndexByOccurrenceAndIdentityKey()
    {
        var definitions = await QueryAsync(postgres.ConnectionString, "SELECT indexdef FROM pg_indexes WHERE tablename = 'assistant_settings_audit'");
        var identity = await QueryAsync(postgres.ConnectionString, "SELECT is_identity || ':' || identity_generation FROM information_schema.columns WHERE table_name = 'assistant_settings_audit' AND column_name = 'id'");

        Assert.Contains(definitions, definition => definition.Contains("ix_assistant_settings_audit_company_id_occurred_at", StringComparison.Ordinal)
            && definition.Contains("occurred_at DESC", StringComparison.Ordinal));
        Assert.Equal(["YES:ALWAYS"], identity);
    }

    // Una línea por tabla con sus columnas, tipos y nulabilidad: sirve para comprobar que una migración no altera tablas existentes.
    private static async Task<List<string>> TableShapeAsync(string connectionString)
        => await QueryAsync(
            connectionString,
            "SELECT table_name || ':' || string_agg(column_name || '/' || data_type || '/' || is_nullable, ',' ORDER BY column_name) "
            + "FROM information_schema.columns WHERE table_schema = 'public' GROUP BY table_name ORDER BY table_name");

    [Fact]
    public async Task Check_RejectsUnknownType()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(Guid.NewGuid(), "2026-10-12T08:00:00Z", "2026-10-12T09:00:00Z", "Vacaciones"));

        Assert.Equal("ck_time_entries_type", error.ConstraintName);
    }

    [Theory]
    [InlineData("Work")]
    [InlineData("Break")]
    [InlineData("MealBreak")]
    public async Task Check_AcceptsEachKnownType(string type)
        => await InsertAsync(Guid.NewGuid(), "2026-10-12T08:00:00Z", "2026-10-12T09:00:00Z", type);

    [Fact]
    public async Task Insert_WithoutType_DefaultsToWork()
    {
        var employeeId = Guid.NewGuid();
        await InsertAsync(employeeId, "2026-10-12T08:00:00Z", "2026-10-12T09:00:00Z");

        var types = await QueryAsync(postgres.ConnectionString, $"SELECT type FROM time_entries WHERE employee_id = '{employeeId}'");

        Assert.Equal(["Work"], types);
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
        Assert.Contains("type:character varying:NO", columns);
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

    private async Task InsertAsync(Guid employeeId, string clockInAt, string? clockOutAt, string? type = null)
    {
        UserSeed.EnsureExists(postgres.ConnectionString, employeeId);
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            type is null
                ? "INSERT INTO time_entries (id, company_id, employee_id, clock_in_at, clock_out_at) VALUES (@id, @company, @employee, @in, @out)"
                : "INSERT INTO time_entries (id, company_id, employee_id, clock_in_at, clock_out_at, type) VALUES (@id, @company, @employee, @in, @out, @type)",
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("company", Company.DefaultId);
        command.Parameters.AddWithValue("employee", employeeId);
        command.Parameters.AddWithValue("in", DateTimeOffset.Parse(clockInAt, System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("out", clockOutAt is null ? DBNull.Value : DateTimeOffset.Parse(clockOutAt, System.Globalization.CultureInfo.InvariantCulture));
        if (type is not null)
        {
            command.Parameters.AddWithValue("type", type);
        }

        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static Task MigrateToAsync(DbContext context, string migrationSuffix)
    {
        var target = context.GetService<IMigrationsAssembly>().Migrations.Keys.Single(id => id.EndsWith(migrationSuffix, StringComparison.Ordinal));
        return context.GetService<IMigrator>().MigrateAsync(target, CancellationToken.None);
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
