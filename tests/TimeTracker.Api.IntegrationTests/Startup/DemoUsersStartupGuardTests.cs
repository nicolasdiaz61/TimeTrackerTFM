using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>
/// Con el registro abierto en producción no puede quedar ningún usuario de demostración (contraseña pública) en la compañía por defecto
/// (startup-configuration, «El registro abierto en producción exige que no existan usuarios demo»). La comprobación corre tras las
/// migraciones y antes de atender peticiones, dentro de un ámbito de la compañía por defecto y sin <c>IgnoreQueryFilters</c>.
/// </summary>
[Collection("Postgres")]
public class DemoUsersStartupGuardTests(PostgresFixture postgres)
{
    private const string Code = "codigo-prueba-0123456789";
    private const string DemoEmail = "luis.empleado@example.test";
    private static readonly Guid OtherCompany = Guid.Parse("0197a1b2-bbbb-7e4f-8a5b-6c7d8e9f0a02");

    [Fact]
    public async Task AnOpenRegistrationInProductionWithADemoUser_FailsNamingTheFlagWithoutEmailsOrCodes()
    {
        var connectionString = await MigratedDatabaseAsync();
        await UserSeed.InsertRawAsync(connectionString, Guid.NewGuid(), DemoEmail);
        using var factory = CreateFactory(connectionString, production: true, registration: true);

        var exception = factory.StartExpectingFailure();

        var messages = StartupFailure.AllMessages(exception);
        Assert.Contains("Registration:Enabled", messages, StringComparison.Ordinal);
        Assert.Contains("usuarios de demostración", messages, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", messages, StringComparison.Ordinal);
        Assert.DoesNotContain("luis", messages, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, messages, StringComparison.Ordinal);
        Assert.Contains(StartupFailure.Flatten(exception), inner => inner is InvalidOperationException);
    }

    [Fact]
    public async Task AnOpenRegistrationInProductionWithoutDemoUsers_Starts_AndTheCheckRunsAfterTheMigrations()
    {
        // Base vacía con las migraciones activadas: si la comprobación corriera antes de migrar, la consulta fallaría por tabla ausente.
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, production: true, registration: true, migrate: true);

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AClosedRegistrationWithDemoUsersInProduction_Starts()
    {
        var connectionString = await MigratedDatabaseAsync();
        await UserSeed.InsertRawAsync(connectionString, Guid.NewGuid(), DemoEmail);
        using var factory = CreateFactory(connectionString, production: true, registration: false);

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnOpenRegistrationOutsideProductionWithDemoUsers_Starts()
    {
        var connectionString = await MigratedDatabaseAsync();
        await UserSeed.InsertRawAsync(connectionString, Guid.NewGuid(), DemoEmail);
        using var factory = CreateFactory(connectionString, production: false, registration: true);

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ADemoDomainEmailInAnotherCompany_IsNotDetected_BecauseOnlyTheDefaultCompanyHasThePublicPassword()
    {
        var connectionString = await MigratedDatabaseAsync();
        await CompanySeed.InsertAsync(connectionString, "otra compañía");
        await ExecuteAsync(connectionString, $"INSERT INTO companies (id, name, name_normalized, created_at) VALUES ('{OtherCompany}', 'Otra', 'otra', now())");
        await UserSeed.InsertRawAsync(connectionString, Guid.NewGuid(), "pablo.empleado@example.test", companyId: OtherCompany);
        using var factory = CreateFactory(connectionString, production: true, registration: true);

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnEmailOfAnotherDomainInTheDefaultCompany_IsNotADemoUser()
    {
        var connectionString = await MigratedDatabaseAsync();
        await UserSeed.InsertRawAsync(connectionString, Guid.NewGuid(), "luis.real@empresa-real.example");
        using var factory = CreateFactory(connectionString, production: true, registration: true);

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheCheck_ActsInsideTheScopeOfTheDefaultCompany()
    {
        var connectionString = await MigratedDatabaseAsync();
        var recorder = new RecordingCompanyScopeFactory();
        using var factory = CreateFactory(connectionString, production: true, registration: true).WithWebHostBuilder(recorder.InstallOn);

        _ = factory.CreateClient();

        Assert.Equal([Company.DefaultId], recorder.Requested);
    }

    private async Task<string> MigratedDatabaseAsync()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var context = postgres.CreateContext(connectionString);
        await context.Database.MigrateAsync();
        return connectionString;
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, bool production, bool registration, bool migrate = false)
        => new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", migrate ? "true" : "false");
            if (production)
            {
                builder.UseEnvironment("Production");
            }

            if (registration)
            {
                builder.UseSetting("Registration:Enabled", "true");
                builder.UseSetting("Registration:InviteCodes", Code);
                builder.UseSetting("ForwardedHeaders:Enabled", "true");
                builder.UseSetting("ForwardedHeaders:Hops", "1");
            }
        });

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
