using Npgsql;

namespace TimeTracker.Api.IntegrationTests.Postgres;

[Collection("Postgres")]
public class PostgresFixtureTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Container_RunsPostgres18()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand("SELECT version()", connection);

        var version = (string?)await command.ExecuteScalarAsync(CancellationToken.None);

        Assert.StartsWith("PostgreSQL 18", version, StringComparison.Ordinal);
    }
}
