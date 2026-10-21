using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// Ensayo local con datos de la migración A (V-11, parte 1): una base con la forma de la de producción antes del cambio
/// (usuarios, fichajes con uno huérfano, configuración del asistente y auditoría) recibe <c>Up A</c> al arrancar la API,
/// que sigue sirviendo el flujo mínimo; después <c>Down A</c> devuelve los datos tal como estaban. Todo en un contenedor
/// propio de la prueba, nunca contra producción.
/// </summary>
[Collection("Postgres")]
public class AddCompaniesRehearsalTests(PostgresFixture postgres)
{
    private const string PreviousMigration = "_AddAssistantAuditImmutability";
    private const string ManagerEmail = "marta.ensayo@example.test";
    private const string EmployeeEmail = "luis.ensayo@example.test";

    private const string BusinessRows =
        "SELECT 'u|' || id || '|' || email || '|' || display_name || '|' || password_hash || '|' || role || '|' || COALESCE(manager_id::text, 'null') FROM users "
        + "UNION ALL SELECT 'e|' || id || '|' || employee_id || '|' || clock_in_at || '|' || COALESCE(clock_out_at::text, 'null') || '|' || type FROM time_entries WHERE clock_in_at < '2026-10-12T08:00:00Z' "
        + "UNION ALL SELECT 's|' || id || '|' || provider || '|' || updated_by FROM assistant_settings "
        + "UNION ALL SELECT 'a|' || id || '|' || occurred_at || '|' || action FROM assistant_settings_audit ORDER BY 1";

    [Fact]
    public async Task UpA_OnProductionShapedData_KeepsTheApiServingAndDownARestoresEverything()
    {
        var connectionString = await SeedPreviousSchemaAsync();
        var before = await QueryAsync(connectionString, BusinessRows);
        Assert.Contains(before, row => row.StartsWith("e|", StringComparison.Ordinal));

        using (var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        }))
        {
            var anonymous = factory.CreateAnonymousClient();
            var manager = await LoginAsync(factory, ManagerEmail);
            var employee = await LoginAsync(factory, EmployeeEmail);

            // La API arrancó sobre el resultado de Up A y el flujo mínimo funciona con los datos previos.
            Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health", CancellationToken.None)).StatusCode);
            var team = await manager.GetAsync("/api/team/members", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, team.StatusCode);
            using (var teamBody = JsonDocument.Parse(await team.Content.ReadAsStringAsync(CancellationToken.None)))
            {
                Assert.Equal(["Luis Ensayo"], teamBody.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("displayName").GetString()));
            }

            Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/time-entries", CancellationToken.None)).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await employee.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/asistente/configuracion", CancellationToken.None)).StatusCode);
        }

        Assert.Equal(["Demo"], await QueryAsync(connectionString, "SELECT name FROM companies"));
        Assert.Equal([Company.DefaultId.ToString()], await QueryAsync(connectionString, "SELECT DISTINCT company_id::text FROM users"));
        Assert.Equal(
            [Company.DefaultId.ToString()],
            await QueryAsync(connectionString, "SELECT DISTINCT company_id::text FROM time_entries"));
        Assert.Equal(
            ["trg_assistant_settings_audit_no_truncate:O", "trg_assistant_settings_audit_no_update_delete:O"],
            await QueryAsync(connectionString, "SELECT tgname || ':' || tgenabled::text FROM pg_trigger WHERE tgrelid = 'assistant_settings_audit'::regclass AND NOT tgisinternal ORDER BY tgname"));

        await using (var context = postgres.CreateContext(connectionString))
        {
            var target = context.GetService<IMigrationsAssembly>().Migrations.Keys.Single(id => id.EndsWith(PreviousMigration, StringComparison.Ordinal));
            await context.GetService<IMigrator>().MigrateAsync(target, CancellationToken.None);
        }

        // Down A: los datos previos intactos (el fichaje del ensayo se creó después y conserva sus valores) y los triggers en pie.
        Assert.Equal(before, await QueryAsync(connectionString, BusinessRows));
        Assert.DoesNotContain("companies", await QueryAsync(connectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"));
        Assert.Equal(
            ["trg_assistant_settings_audit_no_truncate:O", "trg_assistant_settings_audit_no_update_delete:O"],
            await QueryAsync(connectionString, "SELECT tgname || ':' || tgenabled::text FROM pg_trigger WHERE tgrelid = 'assistant_settings_audit'::regclass AND NOT tgisinternal ORDER BY tgname"));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connectionString, "UPDATE assistant_settings_audit SET credential_changed = true"));
    }

    private static async Task<HttpClient> LoginAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = UserSeed.Password }, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync(CancellationToken.None));
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    // Base en el estado anterior al cambio, con la forma de la de producción y un fichaje huérfano a propósito.
    private async Task<string> SeedPreviousSchemaAsync()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            var target = context.GetService<IMigrationsAssembly>().Migrations.Keys.Single(id => id.EndsWith(PreviousMigration, StringComparison.Ordinal));
            await context.GetService<IMigrator>().MigrateAsync(target, CancellationToken.None);
        }

        var manager = Guid.NewGuid();
        var employee = Guid.NewGuid();
        await ExecuteAsync(connectionString, $"INSERT INTO users (id, email, display_name, password_hash, role, manager_id) VALUES ('{manager}', '{ManagerEmail}', 'Marta Ensayo', '{UserSeed.PasswordHash}', 'Manager', NULL), ('{employee}', '{EmployeeEmail}', 'Luis Ensayo', '{UserSeed.PasswordHash}', 'Employee', '{manager}')");
        foreach (var day in Enumerable.Range(1, 9))
        {
            await ExecuteAsync(connectionString, $"INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at, type) VALUES (gen_random_uuid(), '{employee}', '2026-10-0{day}T08:00:00Z', '2026-10-0{day}T16:00:00Z', 'Work')");
        }

        await ExecuteAsync(connectionString, "INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at, type) VALUES (gen_random_uuid(), gen_random_uuid(), '2026-10-10T08:00:00Z', '2026-10-10T09:00:00Z', 'Work')");
        await ExecuteAsync(connectionString, $"INSERT INTO assistant_settings (id, provider, updated_by, updated_at) VALUES (1, 'None', '{manager}', '2026-10-15T10:00:00Z')");
        await ExecuteAsync(connectionString, $"INSERT INTO assistant_settings_audit (occurred_at, actor_id, action, credential_changed, previous, current) VALUES ('2026-10-15T10:00:00Z', '{manager}', 'Saved', false, NULL, '{{\"provider\":\"None\"}}'), ('2026-10-16T10:00:00Z', '{manager}', 'Reset', false, '{{\"provider\":\"None\"}}', NULL)");
        return connectionString;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task<List<string>> QueryAsync(string connectionString, string sql)
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
}
