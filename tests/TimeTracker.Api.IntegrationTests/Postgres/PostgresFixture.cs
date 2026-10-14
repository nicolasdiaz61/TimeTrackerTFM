using Microsoft.EntityFrameworkCore;

using Npgsql;

using Testcontainers.PostgreSql;

using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Postgres;

/// <summary>
/// Un contenedor PostgreSQL real por ejecución, compartido por todas las pruebas de la colección.
/// Requiere Docker en marcha: sin él, falla con un mensaje claro (no se omite en silencio).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Misma imagen que docker-compose.yml.
    public const string Image = "postgres:18-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    /// <summary>Base ya migrada con <c>MigrateAsync</c> (nunca <c>EnsureCreated</c>), compartida por las pruebas.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"No se pudo iniciar el contenedor {Image}. Las pruebas de integración requieren Docker en marcha.",
                exception);
        }

        await using var context = CreateContext(ConnectionString);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public TimeTrackerDbContext CreateContext(string connectionString)
        => new(new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(connectionString).Options);

    /// <summary>Crea una base vacía (sin migrar) en el mismo servidor y devuelve su cadena de conexión.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = $"empty_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }
}
