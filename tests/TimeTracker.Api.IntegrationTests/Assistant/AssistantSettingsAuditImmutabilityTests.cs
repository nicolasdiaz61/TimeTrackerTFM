using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// DD-9 reforzado en la base de datos: la tabla de auditoría solo admite inserción aunque alguien con acceso directo (o un error
/// futuro del código) intente modificarla. Un trigger vale también para el propietario de la tabla, que un REVOKE no frena.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantSettingsAuditImmutabilityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string PreviousMigration = "20261020001902_AddAssistantSettings";
    private const string InsertAudit = "INSERT INTO assistant_settings_audit (occurred_at, actor_id, action, credential_changed) VALUES (now(), gen_random_uuid(), 'Saved', false)";

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var context = postgres.CreateContext(_connectionString);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Insert_StillWorks()
    {
        await ExecuteAsync(InsertAudit);

        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM assistant_settings_audit"));
    }

    [Theory]
    [InlineData("UPDATE assistant_settings_audit SET credential_changed = true")]
    [InlineData("DELETE FROM assistant_settings_audit")]
    [InlineData("TRUNCATE assistant_settings_audit")]
    public async Task UpdateDeleteAndTruncate_AreRejectedAndKeepTheRow(string sql)
    {
        await ExecuteAsync(InsertAudit);

        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql));

        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Contains("solo admite inserción", error.MessageText, StringComparison.Ordinal);
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM assistant_settings_audit"));
        Assert.Equal(false, await ScalarAsync("SELECT credential_changed FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task OnlyTheAuditTableGetsATrigger_SoTheOtherTablesAreUntouched()
    {
        Assert.Equal(["assistant_settings_audit"], await ColumnAsync(
            "SELECT DISTINCT c.relname FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid WHERE NOT t.tgisinternal"));
        await ExecuteAsync("UPDATE assistant_settings SET provider = 'None'");
    }

    [Fact]
    public async Task Down_RemovesTheTriggersAndTheFunction_AndRestoresUpdateAndDelete()
    {
        await ExecuteAsync(InsertAudit);

        await using (var context = postgres.CreateContext(_connectionString))
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        }

        Assert.Empty(await ColumnAsync("SELECT tgname FROM pg_trigger WHERE NOT tgisinternal"));
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM pg_proc WHERE proname = 'assistant_settings_audit_append_only'"));
        await ExecuteAsync("UPDATE assistant_settings_audit SET credential_changed = true");
        await ExecuteAsync("DELETE FROM assistant_settings_audit");
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM information_schema.tables WHERE table_name = 'assistant_settings_audit'"));
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(CancellationToken.None);
    }

    private async Task<List<string>> ColumnAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
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
