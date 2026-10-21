using Npgsql;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>Crea compañías y fichajes de prueba por SQL (sin pasar por el dominio, que aún no expone el alta de compañías).</summary>
internal static class CompanySeed
{
    public static async Task<Guid> InsertAsync(string connectionString, string? name = null)
    {
        var id = Guid.NewGuid();
        var unique = name ?? $"compania {id:N}";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            "INSERT INTO companies (id, name, name_normalized, created_at) VALUES (@id, @name, @normalized, now())",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", unique);
        command.Parameters.AddWithValue("normalized", unique.ToLowerInvariant());
        await command.ExecuteNonQueryAsync(CancellationToken.None);
        return id;
    }

    /// <summary>Inserta un fichaje cerrado por SQL con la compañía y el empleado indicados.</summary>
    public static async Task InsertTimeEntryAsync(string connectionString, Guid companyId, Guid employeeId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            "INSERT INTO time_entries (id, company_id, employee_id, clock_in_at, clock_out_at, type) VALUES (@id, @company, @employee, '2026-10-12T08:00:00Z', '2026-10-12T09:00:00Z', 'Work')",
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("company", companyId);
        command.Parameters.AddWithValue("employee", employeeId);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
