using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.Persistence;

[Collection("Postgres")]
public class TimeEntriesForeignKeyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task TimeEntries_HaveNoForeignKeyAndKeepPrimaryKeyAndCheck()
    {
        var constraints = await QueryAsync(
            "SELECT contype::text || ':' || conname FROM pg_constraint WHERE conrelid = 'time_entries'::regclass");

        Assert.DoesNotContain(constraints, constraint => constraint.StartsWith("f:", StringComparison.Ordinal));
        Assert.Contains("p:PK_time_entries", constraints);
        Assert.Contains("c:ck_time_entries_clock_out_after_clock_in", constraints);
    }

    [Fact]
    public async Task OrphanEntry_IsAccepted()
    {
        var orphanEmployeeId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            "INSERT INTO time_entries (id, employee_id, clock_in_at) VALUES (@id, @employee, now())", connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("employee", orphanEmployeeId);

        Assert.Equal(1, await command.ExecuteNonQueryAsync(CancellationToken.None));
    }

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
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
