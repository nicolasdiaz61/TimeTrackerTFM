using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

namespace TimeTracker.Api.IntegrationTests.Persistence;

[Collection("Postgres")]
public class UserConstraintsTests(PostgresFixture postgres)
{
    private static string NewEmail() => $"usuario.{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task DuplicateEmail_IsRejectedWithUniqueViolation()
    {
        var email = NewEmail();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), email);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), email));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_users_email", error.ConstraintName);
    }

    [Fact]
    public async Task UnknownManagerId_IsRejectedWithForeignKeyViolation()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), NewEmail(), managerId: Guid.NewGuid()));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_users_users_manager_id", error.ConstraintName);
    }

    [Fact]
    public async Task DeletingAManagerWithReports_IsRestricted()
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, managerId, NewEmail(), "Manager");
        await UserSeed.InsertRawAsync(postgres.ConnectionString, reportId, NewEmail(), managerId: managerId);

        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("DELETE FROM users WHERE id = @id", managerId));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(managerId, await ScalarAsync("SELECT manager_id FROM users WHERE id = @id", reportId));
    }

    [Fact]
    public async Task ManagerEqualToId_IsRejectedByCheck()
    {
        var id = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, id, NewEmail(), managerId: id));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_users_manager_not_self", error.ConstraintName);
    }

    [Fact]
    public async Task UnknownRole_IsRejectedByCheck()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), NewEmail(), "Admin"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_users_role", error.ConstraintName);
    }

    [Fact]
    public async Task EmailWithUppercase_IsRejectedByCheck()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), "Luis.Empleado@example.test"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_users_email_lowercase", error.ConstraintName);
    }

    private async Task ExecuteAsync(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private async Task<object?> ScalarAsync(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync(CancellationToken.None);
    }
}
