using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>Restricciones de la tabla <c>companies</c> en la base migrada (spec company-isolation).</summary>
[Collection("Postgres")]
public class CompaniesTableTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Companies_HasSixRequiredColumnsWithTheExpectedDefaults()
    {
        var columns = await QueryAsync(
            "SELECT column_name || ':' || is_nullable || ':' || COALESCE(column_default, 'sin') FROM information_schema.columns WHERE table_name = 'companies' ORDER BY column_name");

        Assert.Equal(
            [
                "created_at:NO:sin",
                "id:NO:sin",
                "name:NO:sin",
                "name_normalized:NO:sin",
                "status:NO:'Active'::character varying",
                "uses_platform_assistant:NO:false",
            ],
            columns);
    }

    [Fact]
    public async Task DefaultCompany_ExistsOnceActiveAndWithThePlatformAssistant()
    {
        var rows = await QueryAsync($"SELECT status || ':' || uses_platform_assistant FROM companies WHERE id = '{Company.DefaultId}'");

        Assert.Equal(["Active:true"], rows);
    }

    [Fact]
    public async Task InsertingACompanyWithoutOptionalColumns_UsesTheDefaults()
    {
        var id = Guid.NewGuid();

        await ExecuteAsync($"INSERT INTO companies (id, name, name_normalized, created_at) VALUES ('{id}', 'Registrada {id}', 'registrada {id}', now())");

        Assert.Equal(["Active:false"], await QueryAsync($"SELECT status || ':' || uses_platform_assistant FROM companies WHERE id = '{id}'"));
    }

    [Fact]
    public async Task DuplicateNormalizedName_IsRejectedWithUniqueViolation()
    {
        var normalized = $"acme norte {Guid.NewGuid():N}";
        await ExecuteAsync($"INSERT INTO companies (id, name, name_normalized, created_at) VALUES (gen_random_uuid(), 'Acme Norte', '{normalized}', now())");

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync($"INSERT INTO companies (id, name, name_normalized, created_at) VALUES (gen_random_uuid(), 'ACME  norte', '{normalized}', now())"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_companies_name_normalized", error.ConstraintName);
    }

    [Fact]
    public async Task StatusOutsideTheCatalog_IsRejectedWithCheckViolation()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync("INSERT INTO companies (id, name, name_normalized, status, created_at) VALUES (gen_random_uuid(), 'Borrada', 'borrada', 'Deleted', now())"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_companies_status", error.ConstraintName);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task BlankName_IsRejectedWithCheckViolation(string name)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync($"INSERT INTO companies (id, name, name_normalized, created_at) VALUES (gen_random_uuid(), '{name}', 'x{Guid.NewGuid():N}', now())"));

        Assert.Equal("ck_companies_name_length", error.ConstraintName);
    }

    [Fact]
    public async Task Suspended_IsAccepted()
        => await ExecuteAsync($"INSERT INTO companies (id, name, name_normalized, status, created_at) VALUES (gen_random_uuid(), 'Suspendida', 'suspendida {Guid.NewGuid():N}', 'Suspended', now())");

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
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
