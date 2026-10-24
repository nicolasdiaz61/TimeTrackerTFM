using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>
/// Utilidades de las pruebas de migración con datos: migrar hasta un punto por sufijo, ejecutar SQL y sembrar la base
/// «tras A» (esquema expandido, con usuarios, fichajes, la fila de configuración con clave cifrada v1 y auditoría).
/// </summary>
internal static class MigrationDatabase
{
    public const string AddCompanies = "_AddCompanies";
    public const string EnforceCompanyIsolation = "_EnforceCompanyIsolation";
    public const string AddAssistantAuditImmutability = "_AddAssistantAuditImmutability";
    public const string AddWorkSchedules = "_AddWorkSchedules";
    public const string AddGroqProvider = "_AddGroqProvider";
    public const string AddWorkScheduleEndTime = "_AddWorkScheduleEndTime";

    public static readonly string DefaultCompany = Company.DefaultId.ToString();

    public static Task MigrateToAsync(DbContext context, string migrationSuffix)
    {
        var target = context.GetService<IMigrationsAssembly>().Migrations.Keys.Single(id => id.EndsWith(migrationSuffix, StringComparison.Ordinal));
        return context.GetService<IMigrator>().MigrateAsync(target, CancellationToken.None);
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    public static async Task<List<string>> QueryAsync(string connectionString, string sql)
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

    /// <summary>Todas las excepciones encadenadas, para buscar la <see cref="PostgresException"/> que EF envuelve.</summary>
    public static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            yield return current;
        }
    }

    /// <summary>Base migrada hasta A con datos de negocio; <paramref name="withOrphan"/> añade un fichaje cuyo empleado no existe.</summary>
    public static async Task<SeededDatabase> SeedAfterAAsync(PostgresFixture postgres, bool withOrphan)
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await MigrateToAsync(context, AddCompanies);
        }

        var manager = Guid.NewGuid();
        var employee = Guid.NewGuid();
        await ExecuteAsync(connectionString, $"INSERT INTO users (id, company_id, email, display_name, password_hash, role, manager_id) VALUES ('{manager}', '{DefaultCompany}', 'marta.despues@example.test', 'Marta', 'hash-marta', 'Manager', NULL), ('{employee}', '{DefaultCompany}', 'luis.despues@example.test', 'Luis', 'hash-luis', 'Employee', '{manager}')");
        await ExecuteAsync(connectionString, $"INSERT INTO time_entries (id, company_id, employee_id, clock_in_at, clock_out_at, type) VALUES (gen_random_uuid(), '{DefaultCompany}', '{employee}', '2026-10-12T08:00:00Z', '2026-10-12T12:00:00Z', 'Work'), (gen_random_uuid(), '{DefaultCompany}', '{employee}', '2026-10-13T08:00:00Z', NULL, 'Work')");
        if (withOrphan)
        {
            await ExecuteAsync(connectionString, $"INSERT INTO time_entries (id, company_id, employee_id, clock_in_at, clock_out_at, type) VALUES (gen_random_uuid(), '{DefaultCompany}', gen_random_uuid(), '2026-10-14T08:00:00Z', '2026-10-14T09:00:00Z', 'Break')");
        }

        await ExecuteAsync(connectionString, $"INSERT INTO assistant_settings (id, company_id, provider, model, timeout_seconds, max_output_tokens, effort, credential_ciphertext, credential_nonce, credential_key_version, updated_by, updated_at) VALUES (1, '{DefaultCompany}', 'Anthropic', 'modelo-prueba', 30, 800, 'low', decode('0102030405060708', 'hex'), decode('000102030405060708090a0b', 'hex'), 1, '{manager}', '2026-10-15T10:00:00Z')");
        await ExecuteAsync(connectionString, $"INSERT INTO assistant_settings_audit (occurred_at, actor_id, action, credential_changed, previous, current) VALUES ('2026-10-15T10:00:00Z', '{manager}', 'Saved', true, NULL, '{{\"provider\":\"Anthropic\"}}'), ('2026-10-16T10:00:00Z', '{manager}', 'Reset', false, '{{\"provider\":\"Anthropic\"}}', NULL)");
        return new SeededDatabase(connectionString, manager, employee);
    }

    /// <summary>
    /// Base en el estado anterior al cambio (sin compañías), con la forma de la de producción: usuarios, fichajes, la fila de
    /// configuración y auditoría; <paramref name="withOrphan"/> añade un fichaje cuyo empleado no existe.
    /// </summary>
    public static async Task<SeededDatabase> SeedBeforeAAsync(PostgresFixture postgres, bool withOrphan)
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await MigrateToAsync(context, AddAssistantAuditImmutability);
        }

        var manager = Guid.NewGuid();
        var employee = Guid.NewGuid();
        await ExecuteAsync(connectionString, $"INSERT INTO users (id, email, display_name, password_hash, role, manager_id) VALUES ('{manager}', 'marta.antes@example.test', 'Marta', 'hash-marta', 'Manager', NULL), ('{employee}', 'luis.antes@example.test', 'Luis', 'hash-luis', 'Employee', '{manager}')");
        await ExecuteAsync(connectionString, $"INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at, type) VALUES (gen_random_uuid(), '{employee}', '2026-10-12T08:00:00Z', '2026-10-12T12:00:00Z', 'Work')");
        if (withOrphan)
        {
            await ExecuteAsync(connectionString, "INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at, type) VALUES (gen_random_uuid(), gen_random_uuid(), '2026-10-14T08:00:00Z', '2026-10-14T09:00:00Z', 'Break')");
        }

        await ExecuteAsync(connectionString, $"INSERT INTO assistant_settings (id, provider, model, updated_by, updated_at) VALUES (1, 'Ollama', 'modelo-guardado', '{manager}', '2026-10-15T10:00:00Z')");
        await ExecuteAsync(connectionString, $"INSERT INTO assistant_settings_audit (occurred_at, actor_id, action, credential_changed, previous, current) VALUES ('2026-10-15T10:00:00Z', '{manager}', 'Saved', false, NULL, '{{\"provider\":\"Ollama\"}}')");
        return new SeededDatabase(connectionString, manager, employee);
    }

    public sealed record SeededDatabase(string ConnectionString, Guid Manager, Guid Employee);
}
