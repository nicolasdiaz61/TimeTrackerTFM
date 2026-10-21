using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Support.MigrationDatabase;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// V-3: la vía manual de producción aplica el script de <c>dotnet ef migrations script --idempotent</c>. La migración B debe
/// comportarse igual por esa vía que con el migrador de EF: aborta ante huérfanos o varias compañías sin dejar cambios parciales
/// (cada sentencia del script va envuelta en un bloque <c>DO</c> condicional, que contiene al <c>DO</c> propio de la comprobación).
/// </summary>
[Collection("Postgres")]
public class EnforceCompanyIsolationScriptTests(PostgresFixture postgres)
{
    [Fact]
    public async Task TheIdempotentScriptOfUpB_WithAnOrphanEntry_AbortsAndLeavesNoPartialChange()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: true);
        var oldUser = Guid.NewGuid();
        await ExecuteAsync(database.ConnectionString, $"INSERT INTO users (id, email, display_name, password_hash, role) VALUES ('{oldUser}', 'codigo.anterior@example.test', 'Anterior', 'hash', 'Employee')");
        var script = Script(database.ConnectionString, AddCompanies, EnforceCompanyIsolation);

        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, script));

        Assert.Contains("time_entries huérfanos: 1", error.MessageText, StringComparison.Ordinal);
        // El relleno residual anterior a la comprobación se deshizo con el resto: la fila sigue sin compañía.
        Assert.Equal(["null"], await QueryAsync(database.ConnectionString, $"SELECT COALESCE(company_id::text, 'null') FROM users WHERE id = '{oldUser}'"));
        Assert.EndsWith(AddCompanies, Assert.Single(await QueryAsync(database.ConnectionString, "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"")), StringComparison.Ordinal);
        Assert.Equal(["PK_assistant_settings:id"], await QueryAsync(database.ConnectionString, "SELECT c.conname || ':' || a.attname FROM pg_constraint c JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey) WHERE c.conrelid = 'assistant_settings'::regclass AND c.contype = 'p'"));
        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM pg_proc WHERE proname LIKE 'enforce_company_isolation%'"));
    }

    [Fact]
    public async Task TheIdempotentScriptOfUpB_WithoutOrphans_AppliesAndCanBeRunAgainWithoutChanges()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        var script = Script(database.ConnectionString, AddCompanies, EnforceCompanyIsolation);

        await ExecuteAsync(database.ConnectionString, script);
        await ExecuteAsync(database.ConnectionString, script);

        Assert.EndsWith(EnforceCompanyIsolation, Assert.Single(await QueryAsync(database.ConnectionString, "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"")), StringComparison.Ordinal);
        Assert.Equal(["NO", "NO", "NO"], await QueryAsync(database.ConnectionString, "SELECT is_nullable FROM information_schema.columns WHERE column_name = 'company_id' AND table_name IN ('users', 'time_entries', 'assistant_settings') ORDER BY table_name"));
        Assert.Equal(["0"], await QueryAsync(database.ConnectionString, "SELECT count(*)::text FROM pg_proc WHERE proname LIKE 'enforce_company_isolation%'"));
    }

    [Fact]
    public async Task TheIdempotentScriptOfDownB_AbortsWithTwoCompaniesAndRestoresTheSchemaWithOne()
    {
        var database = await SeedAfterAAsync(postgres, withOrphan: false);
        await ExecuteAsync(database.ConnectionString, Script(database.ConnectionString, AddCompanies, EnforceCompanyIsolation));
        var down = Script(database.ConnectionString, EnforceCompanyIsolation, AddCompanies);
        await ExecuteAsync(database.ConnectionString, "INSERT INTO companies (id, name, name_normalized, created_at) VALUES (gen_random_uuid(), 'Segunda', 'segunda', now())");

        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(database.ConnectionString, down));

        Assert.Contains("compañías: 2", error.MessageText, StringComparison.Ordinal);
        Assert.EndsWith(EnforceCompanyIsolation, Assert.Single(await QueryAsync(database.ConnectionString, "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"")), StringComparison.Ordinal);
        Assert.Equal(["PK_assistant_settings:company_id"], await QueryAsync(database.ConnectionString, "SELECT c.conname || ':' || a.attname FROM pg_constraint c JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey) WHERE c.conrelid = 'assistant_settings'::regclass AND c.contype = 'p'"));

        await ExecuteAsync(database.ConnectionString, "DELETE FROM companies WHERE name = 'Segunda'");
        await ExecuteAsync(database.ConnectionString, down);

        Assert.EndsWith(AddCompanies, Assert.Single(await QueryAsync(database.ConnectionString, "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"")), StringComparison.Ordinal);
        Assert.Equal(["PK_assistant_settings:id"], await QueryAsync(database.ConnectionString, "SELECT c.conname || ':' || a.attname FROM pg_constraint c JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey) WHERE c.conrelid = 'assistant_settings'::regclass AND c.contype = 'p'"));
    }

    // Script idempotente entre dos migraciones, como lo genera `dotnet ef migrations script --idempotent`.
    private string Script(string connectionString, string fromSuffix, string toSuffix)
    {
        using var context = postgres.CreateContext(connectionString);
        var ids = context.GetService<IMigrationsAssembly>().Migrations.Keys;
        var from = ids.Single(id => id.EndsWith(fromSuffix, StringComparison.Ordinal));
        var to = ids.Single(id => id.EndsWith(toSuffix, StringComparison.Ordinal));
        return context.GetService<IMigrator>().GenerateScript(from, to, MigrationsSqlGenerationOptions.Idempotent);
    }
}
