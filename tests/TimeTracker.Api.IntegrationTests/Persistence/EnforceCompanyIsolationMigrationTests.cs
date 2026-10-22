using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Support.MigrationDatabase;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// Migración B «contraer» (DD-4): comprueba los fichajes huérfanos antes de tocar nada, rellena lo residual, hace obligatorio
/// <c>company_id</c>, crea las claves foráneas compuestas, pasa la clave primaria de <c>assistant_settings</c> a <c>company_id</c>
/// y retira el <c>DEFAULT</c> de la auditoría sin tocar sus triggers.
/// </summary>
[Collection("Postgres")]
public class EnforceCompanyIsolationMigrationTests(PostgresFixture postgres)
{
    private static readonly string[] BusinessTables = ["users", "time_entries", "assistant_settings", "assistant_settings_audit"];

    private const string UserRows =
        "SELECT id || '|' || company_id || '|' || email || '|' || display_name || '|' || password_hash || '|' || role || '|' || COALESCE(manager_id::text, 'null') || '|' || must_change_password FROM users ORDER BY email";

    private const string EntryRows =
        "SELECT id || '|' || company_id || '|' || employee_id || '|' || to_char(clock_in_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') || '|' || COALESCE(to_char(clock_out_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS'), 'null') || '|' || type FROM time_entries ORDER BY id";

    // Sin la columna id (existe antes de B y no después): la clave primaria deja de ser el singleton.
    private const string SettingsRow =
        "SELECT company_id || '|' || provider || '|' || model || '|' || timeout_seconds || '|' || max_output_tokens || '|' || effort || '|' || encode(credential_ciphertext, 'hex') || '|' || encode(credential_nonce, 'hex') || '|' || credential_key_version || '|' || updated_by FROM assistant_settings";

    private const string AuditRows =
        "SELECT id || '|' || company_id || '|' || occurred_at AT TIME ZONE 'UTC' || '|' || actor_id || '|' || action || '|' || credential_changed || '|' || COALESCE(previous::text, 'null') || '|' || COALESCE(current::text, 'null') FROM assistant_settings_audit ORDER BY id";

    private const string TriggerState =
        "SELECT tgname || ':' || tgenabled::text FROM pg_trigger WHERE tgrelid = 'assistant_settings_audit'::regclass AND NOT tgisinternal ORDER BY tgname";

    private static readonly string[] ExpectedTriggers = ["trg_assistant_settings_audit_no_truncate:O", "trg_assistant_settings_audit_no_update_delete:O"];

    [Fact]
    public async Task Up_WithAnOrphanEntry_FailsNamingTheNumberOfOrphansAndLeavesTheDatabaseAsAfterA()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: true);
        var snapshot = await SnapshotAsync(database.ConnectionString);
        await using var context = postgres.CreateContext(database.ConnectionString);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.MigrateAsync(CancellationToken.None));

        var error = Assert.Single(Chain(failure).OfType<PostgresException>());
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Contains("time_entries huérfanos: 1", error.MessageText, StringComparison.Ordinal);
        Assert.DoesNotContain(database.Employee.ToString(), error.MessageText, StringComparison.Ordinal);
        Assert.Equal(snapshot, await SnapshotAsync(database.ConnectionString));
        Assert.Equal(["YES", "YES", "YES"], await QueryAsync(database.ConnectionString, "SELECT is_nullable FROM information_schema.columns WHERE column_name = 'company_id' AND table_name IN ('users', 'time_entries', 'assistant_settings') ORDER BY table_name"));
        Assert.Equal(["PK_assistant_settings:id"], await QueryAsync(database.ConnectionString, PrimaryKeyOfAssistantSettings));
        Assert.EndsWith(AddCompanies, Assert.Single(await QueryAsync(database.ConnectionString, "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Up_WithTwoOrphanEntries_CountsBothAndASecondRunAfterFixingThemSucceeds()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: true);
        await ExecuteAsync(database.ConnectionString, $"INSERT INTO time_entries (id, company_id, employee_id, clock_in_at, type) VALUES (gen_random_uuid(), '{DefaultCompany}', gen_random_uuid(), '2026-10-15T08:00:00Z', 'Work')");
        await using var context = postgres.CreateContext(database.ConnectionString);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.MigrateAsync(CancellationToken.None));
        Assert.Contains("time_entries huérfanos: 2", Assert.Single(Chain(failure).OfType<PostgresException>()).MessageText, StringComparison.Ordinal);

        // Corregido el huérfano (decisión humana previa), la misma migración se aplica.
        await ExecuteAsync(database.ConnectionString, "DELETE FROM time_entries WHERE employee_id NOT IN (SELECT id FROM users)");
        await context.Database.MigrateAsync(CancellationToken.None);

        Assert.Equal(["NO", "NO", "NO"], await QueryAsync(database.ConnectionString, NullableCompanyColumns));
    }

    [Fact]
    public async Task Up_WithoutOrphans_KeepsEveryRowAndMakesCompanyIdMandatory()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        var users = await QueryAsync(database.ConnectionString, UserRows);
        var entries = await QueryAsync(database.ConnectionString, EntryRows);
        var settings = await QueryAsync(database.ConnectionString, SettingsRow);
        var audit = await QueryAsync(database.ConnectionString, AuditRows);
        var auditVersions = await QueryAsync(database.ConnectionString, "SELECT id || ':' || xmin::text FROM assistant_settings_audit ORDER BY id");
        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        Assert.Equal(users, await QueryAsync(database.ConnectionString, UserRows));
        Assert.Equal(entries, await QueryAsync(database.ConnectionString, EntryRows));
        Assert.Equal(settings, await QueryAsync(database.ConnectionString, SettingsRow));
        Assert.Equal(audit, await QueryAsync(database.ConnectionString, AuditRows));
        Assert.Equal(auditVersions, await QueryAsync(database.ConnectionString, "SELECT id || ':' || xmin::text FROM assistant_settings_audit ORDER BY id"));
        foreach (var table in BusinessTables)
        {
            Assert.Equal([DefaultCompany], await QueryAsync(database.ConnectionString, $"SELECT DISTINCT company_id::text FROM {table}"));
        }

        Assert.Equal(["NO", "NO", "NO"], await QueryAsync(database.ConnectionString, NullableCompanyColumns));
    }

    [Fact]
    public async Task Up_FillsTheRowsThatTheOldCodeWroteWithoutCompanyId()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        var oldUser = Guid.NewGuid();
        await ExecuteAsync(database.ConnectionString, $"INSERT INTO users (id, email, display_name, password_hash, role) VALUES ('{oldUser}', 'codigo.anterior@example.test', 'Anterior', 'hash', 'Employee')");
        await ExecuteAsync(database.ConnectionString, $"INSERT INTO time_entries (id, employee_id, clock_in_at, type) VALUES (gen_random_uuid(), '{oldUser}', now(), 'Work')");
        await ExecuteAsync(database.ConnectionString, "DELETE FROM assistant_settings");
        await ExecuteAsync(database.ConnectionString, $"INSERT INTO assistant_settings (id, provider, updated_by, updated_at) VALUES (1, 'None', '{database.Manager}', now())");
        Assert.Equal(["null"], await QueryAsync(database.ConnectionString, $"SELECT COALESCE(company_id::text, 'null') FROM users WHERE id = '{oldUser}'"));
        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        Assert.Equal([DefaultCompany], await QueryAsync(database.ConnectionString, $"SELECT company_id::text FROM users WHERE id = '{oldUser}'"));
        Assert.Equal([DefaultCompany], await QueryAsync(database.ConnectionString, $"SELECT DISTINCT company_id::text FROM time_entries WHERE employee_id = '{oldUser}'"));
        Assert.Equal([DefaultCompany], await QueryAsync(database.ConnectionString, "SELECT company_id::text FROM assistant_settings"));
    }

    [Fact]
    public async Task Up_RemovesTheAuditDefault_SoAnInsertWithoutCompanyIdIsRejected()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(database.ConnectionString, "INSERT INTO assistant_settings_audit (occurred_at, actor_id, action, credential_changed) VALUES (now(), gen_random_uuid(), 'Saved', false)"));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, error.SqlState);
        Assert.Equal("company_id", error.ColumnName);
    }

    [Fact]
    public async Task Up_KeepsTheAppendOnlyTriggersPresentAndActive()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        Assert.Equal(ExpectedTriggers, await QueryAsync(database.ConnectionString, TriggerState));
        foreach (var statement in new[] { "UPDATE assistant_settings_audit SET credential_changed = true", "DELETE FROM assistant_settings_audit", "TRUNCATE assistant_settings_audit" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, statement));
            Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        }

        Assert.Equal(["2"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task Up_CreatesTheConstraintsAndIndexesOfTheDesignAndRetiresTheSingleCompanyOnes()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using (var context = postgres.CreateContext(database.ConnectionString))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        var constraints = await QueryAsync(database.ConnectionString, "SELECT conrelid::regclass::text || ':' || contype::text || ':' || conname FROM pg_constraint WHERE conrelid::regclass::text IN ('users', 'time_entries', 'assistant_settings', 'assistant_settings_audit') ORDER BY 1");
        Assert.Contains("users:u:ux_users_company_id_id", constraints);
        Assert.Contains("users:f:fk_users_companies_company_id", constraints);
        Assert.Contains("users:f:fk_users_users_company_id_manager_id", constraints);
        Assert.Contains("time_entries:f:fk_time_entries_companies_company_id", constraints);
        Assert.Contains("time_entries:f:fk_time_entries_users_company_id_employee_id", constraints);
        Assert.Contains("assistant_settings:f:fk_assistant_settings_companies_company_id", constraints);
        Assert.Contains("assistant_settings_audit:f:fk_assistant_settings_audit_companies_company_id", constraints);
        Assert.DoesNotContain("users:f:fk_users_users_manager_id", constraints);
        Assert.DoesNotContain("assistant_settings:c:ck_assistant_settings_singleton", constraints);
        Assert.Equal(["PK_assistant_settings:company_id"], await QueryAsync(database.ConnectionString, PrimaryKeyOfAssistantSettings));
        Assert.DoesNotContain("id", await QueryAsync(database.ConnectionString, "SELECT column_name FROM information_schema.columns WHERE table_name = 'assistant_settings'"));

        var indexes = await QueryAsync(database.ConnectionString, "SELECT tablename || ':' || indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename IN ('users', 'time_entries', 'assistant_settings', 'assistant_settings_audit') ORDER BY 1");
        Assert.Contains("users:ix_users_company_id_manager_id", indexes);
        Assert.Contains("time_entries:ix_time_entries_company_id_employee_id", indexes);
        Assert.Contains("assistant_settings_audit:ix_assistant_settings_audit_company_id_occurred_at", indexes);
        Assert.DoesNotContain("users:ix_users_manager_id", indexes);
        Assert.DoesNotContain("assistant_settings_audit:ix_assistant_settings_audit_occurred_at", indexes);
        Assert.DoesNotContain("assistant_settings:ix_assistant_settings_company_id", indexes);
        Assert.Contains(
            await QueryAsync(database.ConnectionString, "SELECT indexdef FROM pg_indexes WHERE indexname = 'ix_assistant_settings_audit_company_id_occurred_at'"),
            definition => definition.Contains("(company_id, occurred_at DESC)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Up_LeavesNoPendingModelChanges()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private const string NullableCompanyColumns =
        "SELECT is_nullable FROM information_schema.columns WHERE column_name = 'company_id' AND table_name IN ('users', 'time_entries', 'assistant_settings') ORDER BY table_name";

    private const string PrimaryKeyOfAssistantSettings =
        "SELECT c.conname || ':' || a.attname FROM pg_constraint c JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey) WHERE c.conrelid = 'assistant_settings'::regclass AND c.contype = 'p'";

    private static async Task<List<string>> SnapshotAsync(string connectionString)
    {
        var rows = new List<string>();
        rows.AddRange(await QueryAsync(connectionString, UserRows));
        rows.AddRange(await QueryAsync(connectionString, EntryRows));
        rows.AddRange(await QueryAsync(connectionString, SettingsRow));
        rows.AddRange(await QueryAsync(connectionString, AuditRows));
        return rows;
    }
}
