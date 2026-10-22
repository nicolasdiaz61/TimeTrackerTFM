using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Support.MigrationDatabase;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// <c>Down</c> de la migración B (DD-4): con una sola compañía devuelve el esquema al de A; con más de una aborta sin
/// modificar nada, porque la separación entre compañías se perdería. La condición es el número de compañías, no el de filas
/// de configuración.
/// </summary>
[Collection("Postgres")]
public class EnforceCompanyIsolationDownTests(PostgresFixture postgres)
{
    private const string SecondCompany = "00000000-0000-7000-8000-0000000000a2";

    private const string TriggerState =
        "SELECT tgname || ':' || tgenabled::text FROM pg_trigger WHERE tgrelid = 'assistant_settings_audit'::regclass AND NOT tgisinternal ORDER BY tgname";

    private static readonly string[] ExpectedTriggers = ["trg_assistant_settings_audit_no_truncate:O", "trg_assistant_settings_audit_no_update_delete:O"];

    private const string Tables = "('companies', 'users', 'time_entries', 'assistant_settings', 'assistant_settings_audit')";

    // Columnas con tipo, nulabilidad y valor por defecto; claves, restricciones e índices con su definición.
    private const string ColumnShape =
        "SELECT table_name || ':' || column_name || '/' || data_type || '/' || is_nullable || '/' || COALESCE(column_default, '') FROM information_schema.columns "
        + $"WHERE table_schema = 'public' AND table_name IN {Tables} ORDER BY 1";

    private const string ConstraintShape =
        $"SELECT conrelid::regclass::text || ':' || conname || ':' || pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid::regclass::text IN {Tables} ORDER BY 1";

    private const string IndexShape =
        $"SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename IN {Tables} ORDER BY 1";

    private const string BusinessRows =
        "SELECT 'u|' || id || '|' || COALESCE(company_id::text, 'null') || '|' || email || '|' || role || '|' || COALESCE(manager_id::text, 'null') FROM users "
        + "UNION ALL SELECT 'e|' || id || '|' || COALESCE(company_id::text, 'null') || '|' || employee_id || '|' || clock_in_at || '|' || type FROM time_entries "
        + "UNION ALL SELECT 's|' || COALESCE(company_id::text, 'null') || '|' || provider || '|' || updated_by FROM assistant_settings "
        + "UNION ALL SELECT 'a|' || id || '|' || COALESCE(company_id::text, 'null') || '|' || action FROM assistant_settings_audit ORDER BY 1";

    [Fact]
    public async Task DownB_WithOneCompanyAndAConfigurationRow_RestoresTheSchemaOfAAndKeepsTheRowReadable()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        var shapeAfterA = AsRestoredByDownB(await ShapeAsync(database.ConnectionString));
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        var rowsAfterB = await QueryAsync(database.ConnectionString, BusinessRows);

        await MigrateToAsync(context, AddCompanies);

        Assert.Equal(shapeAfterA, await ShapeAsync(database.ConnectionString));
        Assert.Equal(rowsAfterB, await QueryAsync(database.ConnectionString, BusinessRows));
        Assert.Equal(["1|Anthropic|modelo-prueba"], await QueryAsync(database.ConnectionString, "SELECT id || '|' || provider || '|' || model FROM assistant_settings"));
        Assert.Equal(["PK_assistant_settings:id"], await QueryAsync(database.ConnectionString, "SELECT c.conname || ':' || a.attname FROM pg_constraint c JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey) WHERE c.conrelid = 'assistant_settings'::regclass AND c.contype = 'p'"));
        Assert.Contains("assistant_settings:ck_assistant_settings_singleton:CHECK ((id = 1))", await QueryAsync(database.ConnectionString, ConstraintShape));
        Assert.Equal(ExpectedTriggers, await QueryAsync(database.ConnectionString, TriggerState));
    }

    [Fact]
    public async Task DownB_WithOneCompanyAndNoConfigurationRow_FinishesWithoutError()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await ExecuteAsync(database.ConnectionString, "DELETE FROM assistant_settings");
        var shapeAfterA = AsRestoredByDownB(await ShapeAsync(database.ConnectionString));
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);

        await MigrateToAsync(context, AddCompanies);

        Assert.Equal(shapeAfterA, await ShapeAsync(database.ConnectionString));
        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM assistant_settings"));
    }

    [Fact]
    public async Task DownB_WithTwoCompaniesAndASingleConfigurationRow_AbortsNamingTheCompaniesAndChangesNothing()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        await AddSecondCompanyAsync(database.ConnectionString);
        var shape = await ShapeAsync(database.ConnectionString);
        var rows = await QueryAsync(database.ConnectionString, BusinessRows);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => MigrateToAsync(context, AddCompanies));

        AssertAbortedBecauseOf(failure, companies: 2);
        Assert.Equal(shape, await ShapeAsync(database.ConnectionString));
        Assert.Equal(rows, await QueryAsync(database.ConnectionString, BusinessRows));
        Assert.Equal(["2", "1"], await QueryAsync(database.ConnectionString, "SELECT (SELECT count(*) FROM companies)::text UNION ALL SELECT (SELECT count(*) FROM assistant_settings)::text"));
        Assert.EndsWith(EnforceCompanyIsolation, Assert.Single(await QueryAsync(database.ConnectionString, "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownB_WithTwoCompaniesAndTwoConfigurationRows_AbortsTheSame()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        await AddSecondCompanyAsync(database.ConnectionString);
        await ExecuteAsync(database.ConnectionString, $"INSERT INTO assistant_settings (company_id, provider, updated_by, updated_at) VALUES ('{SecondCompany}', 'None', gen_random_uuid(), now())");
        var shape = await ShapeAsync(database.ConnectionString);
        var rows = await QueryAsync(database.ConnectionString, BusinessRows);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => MigrateToAsync(context, AddCompanies));

        AssertAbortedBecauseOf(failure, companies: 2);
        Assert.Equal(shape, await ShapeAsync(database.ConnectionString));
        Assert.Equal(rows, await QueryAsync(database.ConnectionString, BusinessRows));
    }

    [Fact]
    public async Task DownB_DependsOnTheNumberOfCompaniesAndNotOnTheConfigurationRows_WithThreeCompaniesAndNoRows()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await ExecuteAsync(database.ConnectionString, "DELETE FROM assistant_settings");
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        await AddSecondCompanyAsync(database.ConnectionString);
        await ExecuteAsync(database.ConnectionString, "INSERT INTO companies (id, name, name_normalized, created_at) VALUES (gen_random_uuid(), 'Tercera', 'tercera', now())");

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => MigrateToAsync(context, AddCompanies));

        AssertAbortedBecauseOf(failure, companies: 3);
        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM assistant_settings"));
    }

    [Fact]
    public async Task TheFullSequence_UpADownBDownAKeepsTheDataAndTheTriggers()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        var usersBefore = await QueryAsync(database.ConnectionString, "SELECT id || '|' || email || '|' || role || '|' || COALESCE(manager_id::text, 'null') FROM users ORDER BY email");
        var entriesBefore = await QueryAsync(database.ConnectionString, "SELECT id || '|' || employee_id || '|' || clock_in_at || '|' || type FROM time_entries ORDER BY id");
        var auditBefore = await QueryAsync(database.ConnectionString, "SELECT id || '|' || actor_id || '|' || action || '|' || occurred_at FROM assistant_settings_audit ORDER BY id");
        await using var context = postgres.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        await MigrateToAsync(context, AddCompanies);

        await MigrateToAsync(context, AddAssistantAuditImmutability);

        Assert.DoesNotContain("companies", await QueryAsync(database.ConnectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"));
        Assert.Equal(usersBefore, await QueryAsync(database.ConnectionString, "SELECT id || '|' || email || '|' || role || '|' || COALESCE(manager_id::text, 'null') FROM users ORDER BY email"));
        Assert.Equal(entriesBefore, await QueryAsync(database.ConnectionString, "SELECT id || '|' || employee_id || '|' || clock_in_at || '|' || type FROM time_entries ORDER BY id"));
        Assert.Equal(auditBefore, await QueryAsync(database.ConnectionString, "SELECT id || '|' || actor_id || '|' || action || '|' || occurred_at FROM assistant_settings_audit ORDER BY id"));
        Assert.Equal(["Anthropic|modelo-prueba"], await QueryAsync(database.ConnectionString, "SELECT provider || '|' || model FROM assistant_settings"));
        Assert.Equal(ExpectedTriggers, await QueryAsync(database.ConnectionString, TriggerState));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, "UPDATE assistant_settings_audit SET credential_changed = true"));
    }

    // El Down de B repone `id smallint DEFAULT 1` (spec): es lo único en que su resultado difiere del esquema que dejó A.
    private static List<string> AsRestoredByDownB(List<string> shapeAfterA)
        => [.. shapeAfterA.Select(line => line == "assistant_settings:id/smallint/NO/" ? "assistant_settings:id/smallint/NO/1" : line)];

    private static void AssertAbortedBecauseOf(Exception failure, int companies)
    {
        var error = Assert.Single(Chain(failure).OfType<PostgresException>());
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Contains($"compañías: {companies}", error.MessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("@", error.MessageText, StringComparison.Ordinal);
    }

    private static Task AddSecondCompanyAsync(string connectionString)
        => ExecuteAsync(connectionString, $"INSERT INTO companies (id, name, name_normalized, created_at) VALUES ('{SecondCompany}', 'Segunda', 'segunda', now())");

    private static async Task<List<string>> ShapeAsync(string connectionString)
    {
        var shape = new List<string>();
        shape.AddRange(await QueryAsync(connectionString, ColumnShape));
        shape.AddRange(await QueryAsync(connectionString, ConstraintShape));
        shape.AddRange(await QueryAsync(connectionString, IndexShape));
        return shape;
    }
}
