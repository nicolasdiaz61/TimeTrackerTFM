using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.Startup;

[Collection("Postgres")]
public class DatabaseMigrationStartupTests(PostgresFixture postgres)
{
    private const string ConnectionStringKey = "ConnectionStrings:Default";
    private const string FlagKey = "Database:ApplyMigrationsOnStartup";

    [Fact]
    public async Task Startup_WithFlagTrue_OnEmptyDatabase_CreatesSchema()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, flag: "true");

        var response = await factory.CreateClient().PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(await TableNameAsync(connectionString));
    }

    [Fact]
    public async Task Startup_WithFlagUnset_DoesNotTouchSchema()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, flag: null);

        _ = factory.CreateClient();

        Assert.Null(await TableNameAsync(connectionString));
    }

    [Fact]
    public void Startup_WithFlagTrue_AndUnreachableServer_Fails()
    {
        // Puerto 1 cerrado y tiempo de espera corto: el arranque falla en lugar de quedarse sin esquema.
        using var factory = CreateFactory("Host=127.0.0.1;Port=1;Database=timetracker;Username=timetracker;Timeout=2", flag: "true");

        var exception = factory.StartExpectingFailure();

        Assert.NotNull(exception);
        Assert.Contains(StartupFailure.Flatten(exception), inner => inner is NpgsqlException);
    }

    [Fact]
    public void Startup_WithFlagTrue_AndBlankConnectionString_FailsValidationBeforeMigrating()
    {
        // V10: la validación de opciones corre antes que la migración (si no, veríamos un fallo de conexión).
        using var factory = CreateFactory("   ", flag: "true");

        var exception = factory.StartExpectingFailure();

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, ConnectionStringKey);
        Assert.DoesNotContain(StartupFailure.Flatten(exception), inner => inner is NpgsqlException);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, string? flag)
        => new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(ConnectionStringKey, connectionString);
            if (flag is not null)
            {
                builder.UseSetting(FlagKey, flag);
            }
        });

    private static async Task<object?> TableNameAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand("SELECT to_regclass('time_entries')::text", connection);
        var result = await command.ExecuteScalarAsync(CancellationToken.None);
        return result is DBNull ? null : result;
    }
}
