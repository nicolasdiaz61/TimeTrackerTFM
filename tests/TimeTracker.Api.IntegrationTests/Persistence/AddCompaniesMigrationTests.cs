using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// Migración A «expandir» (DD-4, DD-5): crea <c>companies</c> y la compañía por defecto, añade <c>company_id</c> relleno
/// con ella y no ejecuta ningún <c>UPDATE</c> sobre la auditoría ni toca sus triggers de solo inserción.
/// </summary>
[Collection("Postgres")]
public class AddCompaniesMigrationTests(PostgresFixture postgres)
{
    private const string PreviousMigration = "_AddAssistantAuditImmutability";
    private const string DefaultCompany = "00000000-0000-7000-8000-000000000001";

    private const string UserRows =
        "SELECT id || '|' || email || '|' || display_name || '|' || password_hash || '|' || role || '|' || COALESCE(manager_id::text, 'null') FROM users ORDER BY email";

    private const string EntryRows =
        "SELECT id || '|' || employee_id || '|' || to_char(clock_in_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') || '|' || COALESCE(to_char(clock_out_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS'), 'null') || '|' || type FROM time_entries ORDER BY id";

    private const string SettingsRow =
        "SELECT id || '|' || provider || '|' || model || '|' || timeout_seconds || '|' || max_output_tokens || '|' || effort || '|' || encode(credential_ciphertext, 'hex') || '|' || encode(credential_nonce, 'hex') || '|' || credential_key_version || '|' || updated_by FROM assistant_settings";

    private const string AuditRows =
        "SELECT id || '|' || occurred_at AT TIME ZONE 'UTC' || '|' || actor_id || '|' || action || '|' || credential_changed || '|' || COALESCE(previous::text, 'null') || '|' || COALESCE(current::text, 'null') FROM assistant_settings_audit ORDER BY id";

    [Fact]
    public async Task Up_OnDatabaseWithData_FinishesAndAssignsEveryRowToTheDefaultCompany()
    {
        var database = await SeedBeforeAsync();
        var usersBefore = await QueryAsync(database.ConnectionString, UserRows);
        var entriesBefore = await QueryAsync(database.ConnectionString, EntryRows);
        var settingsBefore = await QueryAsync(database.ConnectionString, SettingsRow);
        var auditBefore = await QueryAsync(database.ConnectionString, AuditRows);

        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await MigrateToAsync(context, "_AddCompanies");
        }

        Assert.Equal(usersBefore, await QueryAsync(database.ConnectionString, UserRows));
        Assert.Equal(entriesBefore, await QueryAsync(database.ConnectionString, EntryRows));
        Assert.Equal(settingsBefore, await QueryAsync(database.ConnectionString, SettingsRow));
        Assert.Equal(auditBefore, await QueryAsync(database.ConnectionString, AuditRows));
        foreach (var table in new[] { "users", "time_entries", "assistant_settings", "assistant_settings_audit" })
        {
            var companies = await QueryAsync(database.ConnectionString, $"SELECT DISTINCT company_id::text FROM {table}");
            Assert.Equal([DefaultCompany], companies);
        }

        Assert.Equal(["3"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM time_entries WHERE company_id IS NOT NULL"));
        Assert.Equal(["false"], await QueryAsync(database.ConnectionString, "SELECT DISTINCT must_change_password::text FROM users"));
    }

    [Fact]
    public async Task Up_CreatesTheDefaultCompanyMatchingTheDomainConstantAndNormalization()
    {
        var database = await SeedBeforeAsync();

        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await MigrateToAsync(context, "_AddCompanies");
        }

        var row = await QueryAsync(
            database.ConnectionString,
            "SELECT id || '|' || name || '|' || name_normalized || '|' || status || '|' || uses_platform_assistant || '|' || (created_at IS NOT NULL) FROM companies");
        Assert.Equal([$"{Company.DefaultId}|{Company.DefaultName}|{Company.NormalizeName(Company.DefaultName)}|Active|true|true"], row);
        Assert.Equal(DefaultCompany, Company.DefaultId.ToString());
    }

    [Fact]
    public async Task Up_DoesNotUpdateAuditRowsAndKeepsTheAppendOnlyTriggersActive()
    {
        var database = await SeedBeforeAsync();
        var versionsBefore = await QueryAsync(database.ConnectionString, "SELECT id || ':' || xmin::text FROM assistant_settings_audit ORDER BY id");

        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await MigrateToAsync(context, "_AddCompanies");
        }

        // Una fila actualizada cambiaría de versión (xmin): el relleno viene del DEFAULT, que PostgreSQL guarda como metadato.
        Assert.Equal(versionsBefore, await QueryAsync(database.ConnectionString, "SELECT id || ':' || xmin::text FROM assistant_settings_audit ORDER BY id"));
        Assert.Equal(
            ["trg_assistant_settings_audit_no_truncate:O", "trg_assistant_settings_audit_no_update_delete:O"],
            await QueryAsync(database.ConnectionString, "SELECT tgname || ':' || tgenabled::text FROM pg_trigger WHERE tgrelid = 'assistant_settings_audit'::regclass AND NOT tgisinternal ORDER BY tgname"));
        foreach (var statement in new[]
        {
            "UPDATE assistant_settings_audit SET credential_changed = true",
            "DELETE FROM assistant_settings_audit",
            "TRUNCATE assistant_settings_audit",
        })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, statement));
            Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        }

        Assert.Equal(["2"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task Up_AddsTheAuditColumnAsMetadataWithoutRewritingTheTable_V4()
    {
        var database = await SeedBeforeAsync();
        var serverVersion = await QueryAsync(database.ConnectionString, "SELECT current_setting('server_version_num')");
        var fileBefore = await QueryAsync(database.ConnectionString, "SELECT relfilenode::text FROM pg_class WHERE relname = 'assistant_settings_audit'");

        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await MigrateToAsync(context, "_AddCompanies");
        }

        // ADD COLUMN con DEFAULT constante es solo metadato desde PostgreSQL 11: el archivo de la tabla no cambia.
        Assert.True(int.Parse(Assert.Single(serverVersion), System.Globalization.CultureInfo.InvariantCulture) >= 110000);
        Assert.Equal(fileBefore, await QueryAsync(database.ConnectionString, "SELECT relfilenode::text FROM pg_class WHERE relname = 'assistant_settings_audit'"));
    }

    [Fact]
    public async Task Up_KeepsTheOldCodePathWorkingWhenItWritesWithoutCompanyId()
    {
        var database = await SeedBeforeAsync();
        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await MigrateToAsync(context, "_AddCompanies");
        }

        var oldUser = Guid.NewGuid();
        var oldEntry = Guid.NewGuid();
        await ExecuteAsync(database.ConnectionString, $"INSERT INTO users (id, email, display_name, password_hash, role) VALUES ('{oldUser}', 'codigo.anterior@example.test', 'Anterior', 'hash', 'Employee')");
        await ExecuteAsync(database.ConnectionString, $"INSERT INTO time_entries (id, employee_id, clock_in_at, type) VALUES ('{oldEntry}', '{oldUser}', now(), 'Work')");
        await ExecuteAsync(database.ConnectionString, "INSERT INTO assistant_settings_audit (occurred_at, actor_id, action, credential_changed) VALUES (now(), gen_random_uuid(), 'Saved', false)");

        Assert.Equal(["null|false"], await QueryAsync(database.ConnectionString, $"SELECT COALESCE(company_id::text, 'null') || '|' || must_change_password FROM users WHERE id = '{oldUser}'"));
        Assert.Equal(["null"], await QueryAsync(database.ConnectionString, $"SELECT COALESCE(company_id::text, 'null') FROM time_entries WHERE id = '{oldEntry}'"));
        Assert.Equal([DefaultCompany], await QueryAsync(database.ConnectionString, "SELECT company_id::text FROM assistant_settings_audit ORDER BY id DESC LIMIT 1"));
    }

    [Fact]
    public async Task Down_RemovesTheAddedColumnsAndTableKeepingTheRestAndTheTriggers()
    {
        var database = await SeedBeforeAsync();
        var usersBefore = await QueryAsync(database.ConnectionString, UserRows);
        var entriesBefore = await QueryAsync(database.ConnectionString, EntryRows);
        var settingsBefore = await QueryAsync(database.ConnectionString, SettingsRow);
        var auditBefore = await QueryAsync(database.ConnectionString, AuditRows);
        var shapeBefore = await TableShapeAsync(database.ConnectionString);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await MigrateToAsync(context, "_AddCompanies");

        await MigrateToAsync(context, PreviousMigration);

        Assert.Equal(shapeBefore, await TableShapeAsync(database.ConnectionString));
        Assert.DoesNotContain("companies", await QueryAsync(database.ConnectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"));
        Assert.Equal(usersBefore, await QueryAsync(database.ConnectionString, UserRows));
        Assert.Equal(entriesBefore, await QueryAsync(database.ConnectionString, EntryRows));
        Assert.Equal(settingsBefore, await QueryAsync(database.ConnectionString, SettingsRow));
        Assert.Equal(auditBefore, await QueryAsync(database.ConnectionString, AuditRows));
        Assert.Equal(
            ["trg_assistant_settings_audit_no_truncate:O", "trg_assistant_settings_audit_no_update_delete:O"],
            await QueryAsync(database.ConnectionString, "SELECT tgname || ':' || tgenabled::text FROM pg_trigger WHERE tgrelid = 'assistant_settings_audit'::regclass AND NOT tgisinternal ORDER BY tgname"));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, "UPDATE assistant_settings_audit SET credential_changed = true"));
    }

    // Base migrada hasta el estado anterior a este cambio, con usuarios, fichajes (uno huérfano), la fila de
    // configuración con clave cifrada v1 y dos filas de auditoría.
    private async Task<SeededDatabase> SeedBeforeAsync()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await MigrateToAsync(context, PreviousMigration);
        }

        var manager = Guid.NewGuid();
        var employee = Guid.NewGuid();
        await ExecuteAsync(connectionString, $"INSERT INTO users (id, email, display_name, password_hash, role, manager_id) VALUES ('{manager}', 'marta.antes@example.test', 'Marta', 'hash-marta', 'Manager', NULL), ('{employee}', 'luis.antes@example.test', 'Luis', 'hash-luis', 'Employee', '{manager}')");
        await ExecuteAsync(connectionString, $"INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at, type) VALUES (gen_random_uuid(), '{employee}', '2026-10-12T08:00:00Z', '2026-10-12T12:00:00Z', 'Work'), (gen_random_uuid(), '{employee}', '2026-10-13T08:00:00Z', NULL, 'Work'), (gen_random_uuid(), gen_random_uuid(), '2026-10-14T08:00:00Z', '2026-10-14T09:00:00Z', 'Break')");
        await ExecuteAsync(connectionString, $"INSERT INTO assistant_settings (id, provider, model, timeout_seconds, max_output_tokens, effort, credential_ciphertext, credential_nonce, credential_key_version, updated_by, updated_at) VALUES (1, 'Anthropic', 'modelo-prueba', 30, 800, 'low', decode('0102030405060708', 'hex'), decode('000102030405060708090a0b', 'hex'), 1, '{manager}', '2026-10-15T10:00:00Z')");
        await ExecuteAsync(connectionString, $"INSERT INTO assistant_settings_audit (occurred_at, actor_id, action, credential_changed, previous, current) VALUES ('2026-10-15T10:00:00Z', '{manager}', 'Saved', true, NULL, '{{\"provider\":\"Anthropic\"}}'), ('2026-10-16T10:00:00Z', '{manager}', 'Reset', false, '{{\"provider\":\"Anthropic\"}}', NULL)");
        return new SeededDatabase(connectionString);
    }

    // Una línea por tabla con sus columnas, tipos y nulabilidad.
    private static async Task<List<string>> TableShapeAsync(string connectionString)
        => await QueryAsync(
            connectionString,
            "SELECT table_name || ':' || string_agg(column_name || '/' || data_type || '/' || is_nullable, ',' ORDER BY column_name) "
            + "FROM information_schema.columns WHERE table_schema = 'public' GROUP BY table_name ORDER BY table_name");

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

    private sealed record SeededDatabase(string ConnectionString);
}
