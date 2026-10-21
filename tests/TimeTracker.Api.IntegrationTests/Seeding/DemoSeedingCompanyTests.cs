using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Assistant;
using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Seeding;

/// <summary>
/// La siembra de demostración localiza o crea la compañía por defecto de forma idempotente y trabaja solo dentro de
/// ella (DD-22): un correo demo que ya vive en otra compañía impide el arranque sin modificar nada.
/// </summary>
[Collection("Postgres")]
public class DemoSeedingCompanyTests(PostgresFixture postgres)
{
    private const string DemoPassword = "Clave-Demo-De-Prueba-26";
    private const string LuisEmail = "luis.empleado@example.test";
    private const string Snapshot = "SELECT id::text FROM companies UNION ALL SELECT id || '|' || company_id || '|' || email FROM users ORDER BY 1";
    private static readonly Guid OtherCompany = Guid.Parse("0197a1b2-bbbb-7e4f-8a5b-6c7d8e9f0a02");

    [Fact]
    public async Task Seeding_WhenTheDefaultCompanyIsMissing_InsertsItWithTheFixedIdentifier()
    {
        var connectionString = await MigratedDatabaseAsync();
        await ExecuteAsync(connectionString, "DELETE FROM companies");

        using var factory = CreateFactory(connectionString, demoData: false);
        _ = factory.CreateClient();

        Assert.Equal(
            [$"{Company.DefaultId}|Demo|demo|Active|true"],
            await QueryAsync(connectionString, "SELECT id || '|' || name || '|' || name_normalized || '|' || status || '|' || uses_platform_assistant FROM companies"));
    }

    [Fact]
    public async Task Seeding_RunTwice_KeepsOneCompanyAndPutsEveryUserAndEntryInIt()
    {
        var connectionString = await MigratedDatabaseAsync();
        using (var first = CreateFactory(connectionString, demoData: true))
        {
            _ = first.CreateClient();
        }

        var entriesBefore = await QueryAsync(connectionString, "SELECT count(*)::text FROM time_entries");
        using (var second = CreateFactory(connectionString, demoData: true))
        {
            _ = second.CreateClient();
        }

        Assert.Equal(["1"], await QueryAsync(connectionString, $"SELECT count(*)::text FROM companies WHERE id = '{Company.DefaultId}'"));
        Assert.Equal(["4"], await QueryAsync(connectionString, "SELECT count(*)::text FROM users"));
        Assert.Equal([Company.DefaultId.ToString()], await QueryAsync(connectionString, "SELECT DISTINCT company_id::text FROM users"));
        Assert.NotEqual(["0"], entriesBefore);
        Assert.Equal(entriesBefore, await QueryAsync(connectionString, "SELECT count(*)::text FROM time_entries"));
        Assert.Equal([Company.DefaultId.ToString()], await QueryAsync(connectionString, "SELECT DISTINCT company_id::text FROM time_entries"));
    }

    [Fact]
    public async Task Seeding_WithADemoEmailInAnotherCompany_FailsStartupWithoutThePasswordAndChangesNothing()
    {
        var connectionString = await MigratedDatabaseAsync();
        await InsertOtherCompanyAsync(connectionString);
        await UserSeed.InsertRawAsync(connectionString, Guid.NewGuid(), LuisEmail, companyId: OtherCompany);
        var before = await QueryAsync(connectionString, Snapshot);

        using var factory = CreateFactory(connectionString, demoData: false);
        var exception = factory.StartExpectingFailure();

        Assert.Contains(StartupFailure.Flatten(exception), inner => inner is InvalidOperationException && inner.Message.Contains("otra compañía", StringComparison.Ordinal));
        Assert.DoesNotContain(DemoPassword, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
        Assert.Equal(before, await QueryAsync(connectionString, Snapshot));
        Assert.Equal(["1"], await QueryAsync(connectionString, "SELECT count(*)::text FROM users"));
    }

    [Fact]
    public async Task Seeding_WithADemoEmailInAnotherCompany_DoesNotCreateTheOtherDemoUsersFirst()
    {
        var connectionString = await MigratedDatabaseAsync();
        await InsertOtherCompanyAsync(connectionString);
        await UserSeed.InsertRawAsync(connectionString, Guid.NewGuid(), "pablo.empleado@example.test", companyId: OtherCompany);

        using var factory = CreateFactory(connectionString, demoData: false);
        _ = factory.StartExpectingFailure();

        Assert.Equal(["pablo.empleado@example.test"], await QueryAsync(connectionString, "SELECT email FROM users"));
    }

    [Fact]
    public async Task EnsureDefaultAsync_WhenAnotherInstanceInsertsItFirst_TreatsTheUniqueViolationAsExisting()
    {
        var connectionString = await MigratedDatabaseAsync();
        await ExecuteAsync(connectionString, "DELETE FROM companies");
        var race = $"INSERT INTO companies (id, name, name_normalized, status, uses_platform_assistant, created_at) VALUES ('{Company.DefaultId}', 'Demo', 'demo', 'Active', true, now())";
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new RacingSqlInterceptor(connectionString, race))
            .Options;
        await using var context = new TimeTrackerDbContext(options);

        await new CompanyRepository(context, TimeProvider.System).EnsureDefaultAsync(CancellationToken.None);

        Assert.Equal([Company.DefaultId.ToString()], await QueryAsync(connectionString, "SELECT id::text FROM companies"));
    }

    [Fact]
    public async Task EnsureDefaultAsync_WhenItExists_DoesNotChangeIt()
    {
        var connectionString = await MigratedDatabaseAsync();
        await ExecuteAsync(connectionString, $"UPDATE companies SET name = 'Renombrada', name_normalized = 'renombrada', uses_platform_assistant = false WHERE id = '{Company.DefaultId}'");
        await using var context = postgres.CreateContext(connectionString);

        await new CompanyRepository(context, TimeProvider.System).EnsureDefaultAsync(CancellationToken.None);

        Assert.Equal(["Renombrada|false"], await QueryAsync(connectionString, "SELECT name || '|' || uses_platform_assistant FROM companies"));
    }

    private async Task<string> MigratedDatabaseAsync()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var context = postgres.CreateContext(connectionString);
        await context.Database.MigrateAsync();
        return connectionString;
    }

    private static Task InsertOtherCompanyAsync(string connectionString)
        => ExecuteAsync(connectionString, $"INSERT INTO companies (id, name, name_normalized, status, uses_platform_assistant, created_at) VALUES ('{OtherCompany}', 'Otra', 'otra', 'Active', false, now())");

    [Fact]
    public async Task Seeding_ActsInsideTheScopeOfTheDefaultCompany()
    {
        var connectionString = await MigratedDatabaseAsync();
        var recorder = new RecordingCompanyScopeFactory();

        using var factory = CreateFactory(connectionString, demoData: true).WithWebHostBuilder(recorder.InstallOn);
        _ = factory.CreateClient();

        // Una petición por cada sembrador (usuarios y fichajes), siempre con la compañía por defecto.
        Assert.Equal([Company.DefaultId, Company.DefaultId], recorder.Requested);
        Assert.Equal([Company.DefaultId.ToString()], await QueryAsync(connectionString, "SELECT DISTINCT company_id::text FROM users"));
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, bool demoData)
        => new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Seed:DemoUsers", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
            builder.UseSetting("Seed:DemoData", demoData ? "true" : "false");
        });

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
