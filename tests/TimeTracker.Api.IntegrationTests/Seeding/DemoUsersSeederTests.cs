using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Seeding;

[Collection("Postgres")]
public class DemoUsersSeederTests(PostgresFixture postgres)
{
    private const string DemoPassword = "Clave-Demo-De-Prueba-26";
    private const string MartaEmail = "marta.responsable@example.test";

    private static readonly string[] AllEmails =
    [
        "ana.empleada@example.test",
        "luis.empleado@example.test",
        MartaEmail,
        "pablo.empleado@example.test",
    ];

    [Fact]
    public async Task Seeding_CreatesTheFourUsersWithRolesAndManagers()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, seed: "true");
        _ = factory.CreateClient();

        var users = await LoadUsersAsync(connectionString);

        Assert.Equal(AllEmails, users.Select(user => user.Email).Order(StringComparer.Ordinal));
        var marta = users.Single(user => user.Email == MartaEmail);
        Assert.Equal(UserRole.Manager, marta.Role);
        Assert.Null(marta.ManagerId);
        Assert.All(users.Where(user => user.Email is "luis.empleado@example.test" or "ana.empleada@example.test"), user =>
        {
            Assert.Equal(UserRole.Employee, user.Role);
            Assert.Equal(marta.Id, user.ManagerId);
        });
        var pablo = users.Single(user => user.Email == "pablo.empleado@example.test");
        Assert.Equal(UserRole.Employee, pablo.Role);
        Assert.Null(pablo.ManagerId);
    }

    [Fact]
    public async Task Seeding_StoresOnlyVerifiableHashes()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, seed: "true");
        _ = factory.CreateClient();

        var users = await LoadUsersAsync(connectionString);

        Assert.Equal(4, users.Count);
        var hasher = new PasswordHasher<object>();
        Assert.All(users, user =>
        {
            Assert.DoesNotContain(DemoPassword, user.PasswordHash, StringComparison.Ordinal);
            Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(new object(), user.PasswordHash, DemoPassword));
        });
    }

    [Fact]
    public async Task Seeding_RunTwice_KeepsFourUsersWithSameIds()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using (var first = CreateFactory(connectionString, seed: "true"))
        {
            _ = first.CreateClient();
        }

        var before = await LoadUsersAsync(connectionString);

        using (var second = CreateFactory(connectionString, seed: "true"))
        {
            _ = second.CreateClient();
        }

        var after = await LoadUsersAsync(connectionString);
        Assert.Equal(4, after.Count);
        Assert.Equal(before.Select(user => user.Id).Order(), after.Select(user => user.Id).Order());
    }

    [Fact]
    public async Task Seeding_WithExistingMarta_KeepsHerHashAndLinksReportsToHer()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync();
        }

        var existingMartaId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(connectionString, existingMartaId, MartaEmail, role: "Manager");

        using var factory = CreateFactory(connectionString, seed: "true");
        _ = factory.CreateClient();

        var users = await LoadUsersAsync(connectionString);
        Assert.Equal(4, users.Count);
        var marta = users.Single(user => user.Email == MartaEmail);
        Assert.Equal(existingMartaId, marta.Id);
        Assert.Equal(UserSeed.PasswordHash, marta.PasswordHash);
        Assert.All(users.Where(user => user.Email is "luis.empleado@example.test" or "ana.empleada@example.test"), user => Assert.Equal(existingMartaId, user.ManagerId));
    }

    [Fact]
    public async Task Seeding_Disabled_LeavesUsersEmpty()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, seed: null);
        _ = factory.CreateClient();

        Assert.Empty(await LoadUsersAsync(connectionString));
    }

    [Fact]
    public async Task SeededMarta_LogsInAndSeesAnaAndLuisNotPablo()
    {
        // Sin ninguna petición previa: la siembra ya tiene que haber terminado antes de atender la primera (V16).
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, seed: "true");
        var client = factory.CreateAnonymousClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = MartaEmail, password = DemoPassword }, CancellationToken.None);
        Assert.True(login.IsSuccessStatusCode, $"El login de la responsable sembrada devolvió {(int)login.StatusCode}.");
        using var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync(CancellationToken.None));
        client.DefaultRequestHeaders.Authorization = new("Bearer", loginBody.RootElement.GetProperty("accessToken").GetString());

        var team = await client.GetAsync("/api/team/members", CancellationToken.None);

        using var teamBody = JsonDocument.Parse(await team.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(
            ["Ana Ejemplo", "Luis Ejemplo"],
            teamBody.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("displayName").GetString()));
    }

    [Fact]
    public void Seeding_WithUnreachableDatabase_FailsStartup()
    {
        // Sin migraciones: el fallo es del propio sembrador, que no puede conectarse (puerto 1 cerrado, espera corta).
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=timetracker;Username=timetracker;Timeout=2");
            builder.UseSetting("Seed:DemoUsers", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        });

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(StartupFailure.Flatten(exception), inner => inner is Npgsql.NpgsqlException);
    }

    [Fact]
    public async Task Seeding_DoesNotLogThePassword()
    {
        var logs = new CapturingLoggerProvider();
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, seed: "true").WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(logs);
        }));
        _ = factory.CreateClient();

        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(logs.Messages, message => message.Contains(DemoPassword, StringComparison.Ordinal));
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, string? seed)
        => new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
            if (seed is not null)
            {
                builder.UseSetting("Seed:DemoUsers", seed);
                builder.UseSetting("Seed:DemoPassword", DemoPassword);
            }
        });

    private async Task<List<UserAccount>> LoadUsersAsync(string connectionString)
    {
        await using var context = postgres.CreateContext(connectionString);
        return await context.Users.AsNoTracking().ToListAsync(CancellationToken.None);
    }
}
