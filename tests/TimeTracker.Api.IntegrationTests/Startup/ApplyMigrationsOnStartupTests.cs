using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Support.MigrationDatabase;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>
/// Con <c>Database__ApplyMigrationsOnStartup=true</c> y una base con datos previos, las migraciones A y B se aplican antes de
/// atender peticiones; si B aborta (fichajes huérfanos), la API no arranca (spec <c>company-isolation</c>, «Aplicación al arrancar»).
/// </summary>
[Collection("Postgres")]
public class ApplyMigrationsOnStartupTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Startup_OnADatabaseWithPreviousData_AppliesAAndBBeforeServingRequests()
    {
        var database = await SeedBeforeAAsync(postgres, withOrphan: false);
        using var factory = CreateFactory(database.ConnectionString);

        var response = await factory.CreateClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var applied = await QueryAsync(database.ConnectionString, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 3");
        Assert.EndsWith(AddWorkSchedules, applied[0], StringComparison.Ordinal);
        Assert.EndsWith(EnforceCompanyIsolation, applied[1], StringComparison.Ordinal);
        Assert.EndsWith(AddCompanies, applied[2], StringComparison.Ordinal);
        Assert.Equal(["NO", "NO", "NO"], await QueryAsync(database.ConnectionString, "SELECT is_nullable FROM information_schema.columns WHERE column_name = 'company_id' AND table_name IN ('users', 'time_entries', 'assistant_settings') ORDER BY table_name"));
        Assert.Equal([DefaultCompany], await QueryAsync(database.ConnectionString, "SELECT DISTINCT company_id::text FROM time_entries"));
    }

    [Fact]
    public async Task Startup_WhenBAbortsBecauseOfAnOrphanEntry_FailsAndTheDatabaseStaysAsAfterA()
    {
        var database = await SeedBeforeAAsync(postgres, withOrphan: true);
        using var factory = CreateFactory(database.ConnectionString);

        var exception = factory.StartExpectingFailure();

        var error = Assert.Single(StartupFailure.Flatten(exception).OfType<PostgresException>());
        Assert.Contains("time_entries huérfanos: 1", error.MessageText, StringComparison.Ordinal);
        Assert.EndsWith(AddCompanies, Assert.Single(await QueryAsync(database.ConnectionString, "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"")), StringComparison.Ordinal);
        Assert.Equal(["YES", "YES", "YES"], await QueryAsync(database.ConnectionString, "SELECT is_nullable FROM information_schema.columns WHERE column_name = 'company_id' AND table_name IN ('users', 'time_entries', 'assistant_settings') ORDER BY table_name"));
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString)
        => new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        });
}
