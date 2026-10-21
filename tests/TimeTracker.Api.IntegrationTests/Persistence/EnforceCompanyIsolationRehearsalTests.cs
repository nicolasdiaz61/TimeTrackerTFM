using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;

using static TimeTracker.Api.IntegrationTests.Support.MigrationDatabase;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// Ensayo local completo con datos (V-11, parte 2): una base con la forma de la de producción anterior al cambio recibe <c>Up A</c>;
/// B aborta mientras exista un fichaje huérfano; corregido, <c>Up B</c>; <c>Down B</c> con una compañía funciona; con una segunda
/// compañía aborta sin cambios; <c>Down A</c>; y de nuevo <c>Up A</c> y <c>Up B</c> al arrancar la API, que sirve el flujo mínimo.
/// Todo en un contenedor propio de la prueba, nunca contra producción.
/// </summary>
[Collection("Postgres")]
public class EnforceCompanyIsolationRehearsalTests(PostgresFixture postgres)
{
    private const string ManagerEmail = "marta.antes@example.test";
    private const string EmployeeEmail = "luis.antes@example.test";

    private const string BusinessRows =
        "SELECT 'u|' || id || '|' || email || '|' || display_name || '|' || role || '|' || COALESCE(manager_id::text, 'null') FROM users "
        + "UNION ALL SELECT 'e|' || id || '|' || employee_id || '|' || clock_in_at || '|' || COALESCE(clock_out_at::text, 'null') || '|' || type FROM time_entries WHERE clock_in_at < '2026-10-12T00:00:00Z' "
        + "UNION ALL SELECT 's|' || provider || '|' || model || '|' || updated_by FROM assistant_settings "
        + "UNION ALL SELECT 'a|' || id || '|' || occurred_at || '|' || action FROM assistant_settings_audit ORDER BY 1";

    private const string Triggers =
        "SELECT tgname || ':' || tgenabled::text FROM pg_trigger WHERE tgrelid = 'assistant_settings_audit'::regclass AND NOT tgisinternal ORDER BY tgname";

    [Fact]
    public async Task TheFullSequenceOnProductionShapedData_AbortsOnOrphansThenKeepsTheApiServing()
    {
        var database = await SeedBeforeAAsync(postgres, withOrphan: true);
        var connectionString = database.ConnectionString;
        await ExecuteAsync(connectionString, $"UPDATE users SET password_hash = '{UserSeed.PasswordHash}'");
        var before = await QueryAsync(connectionString, BusinessRows);

        await using (var context = postgres.CreateContext(connectionString))
        {
            // Up A y, con el huérfano, B aborta y la base queda como tras A.
            await MigrateToAsync(context, AddCompanies);
            var aborted = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.MigrateAsync(CancellationToken.None));
            Assert.Contains("time_entries huérfanos: 1", Assert.Single(Chain(aborted).OfType<PostgresException>()).MessageText, StringComparison.Ordinal);
            Assert.Equal(["YES"], await QueryAsync(connectionString, "SELECT DISTINCT is_nullable FROM information_schema.columns WHERE table_name = 'time_entries' AND column_name = 'company_id'"));

            // Corregido el huérfano (decisión humana), B se aplica; Down B con una compañía funciona y B se vuelve a aplicar.
            await ExecuteAsync(connectionString, "DELETE FROM time_entries WHERE employee_id NOT IN (SELECT id FROM users)");
            await context.Database.MigrateAsync(CancellationToken.None);
            await MigrateToAsync(context, AddCompanies);
            await context.Database.MigrateAsync(CancellationToken.None);

            // Con una segunda compañía añadida a propósito Down B aborta sin cambiar nada.
            await ExecuteAsync(connectionString, "INSERT INTO companies (id, name, name_normalized, created_at) VALUES (gen_random_uuid(), 'Segunda', 'segunda', now())");
            var rowsBefore = await QueryAsync(connectionString, BusinessRows);
            var refused = await Assert.ThrowsAnyAsync<Exception>(() => MigrateToAsync(context, AddCompanies));
            Assert.Contains("compañías: 2", Assert.Single(Chain(refused).OfType<PostgresException>()).MessageText, StringComparison.Ordinal);
            Assert.Equal(rowsBefore, await QueryAsync(connectionString, BusinessRows));
            Assert.EndsWith(EnforceCompanyIsolation, Assert.Single(await QueryAsync(connectionString, "SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"")), StringComparison.Ordinal);

            // Sin la segunda compañía, Down B y Down A devuelven los datos previos y los triggers.
            await ExecuteAsync(connectionString, "DELETE FROM companies WHERE name = 'Segunda'");
            await MigrateToAsync(context, AddAssistantAuditImmutability);
            Assert.Equal(before, await QueryAsync(connectionString, BusinessRows));
            Assert.DoesNotContain("companies", await QueryAsync(connectionString, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"));
            Assert.Equal(["trg_assistant_settings_audit_no_truncate:O", "trg_assistant_settings_audit_no_update_delete:O"], await QueryAsync(connectionString, Triggers));
        }

        // Up A y Up B de nuevo, esta vez al arrancar la API, que sirve el flujo mínimo.
        using (var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        }))
        {
            var manager = await LoginAsync(factory, ManagerEmail);
            var employee = await LoginAsync(factory, EmployeeEmail);

            Assert.Equal(HttpStatusCode.OK, (await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None)).StatusCode);
            var team = await manager.GetAsync("/api/team/members", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, team.StatusCode);
            using (var teamBody = JsonDocument.Parse(await team.Content.ReadAsStringAsync(CancellationToken.None)))
            {
                Assert.Equal(["Luis"], teamBody.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("displayName").GetString()));
            }

            Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/time-entries", CancellationToken.None)).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await employee.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None)).StatusCode);
            var settings = await manager.GetAsync("/api/asistente/configuracion", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
            using var settingsBody = JsonDocument.Parse(await settings.Content.ReadAsStringAsync(CancellationToken.None));
            Assert.Equal("Marta", settingsBody.RootElement.GetProperty("updatedBy").GetString());
        }

        Assert.Equal(["Demo"], await QueryAsync(connectionString, "SELECT name FROM companies"));
        Assert.Equal([Company.DefaultId.ToString()], await QueryAsync(connectionString, "SELECT DISTINCT company_id::text FROM time_entries"));
        Assert.Equal(["trg_assistant_settings_audit_no_truncate:O", "trg_assistant_settings_audit_no_update_delete:O"], await QueryAsync(connectionString, Triggers));
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = UserSeed.Password }, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync(CancellationToken.None));
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.RootElement.GetProperty("accessToken").GetString());
        return client;
    }
}
