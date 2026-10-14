using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.Persistence;

[Collection("Postgres")]
public class UserSchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Users_Table_HasSixColumnsAndOnlyManagerIdIsNullable()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            "SELECT column_name || ':' || is_nullable FROM information_schema.columns WHERE table_name = 'users' ORDER BY column_name",
            connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var columns = new List<string>();
        while (await reader.ReadAsync(CancellationToken.None))
        {
            columns.Add(reader.GetString(0));
        }

        Assert.Equal(
            ["display_name:NO", "email:NO", "id:NO", "manager_id:YES", "password_hash:NO", "role:NO"],
            columns);
    }
}
