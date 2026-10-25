using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.Registration;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>
/// Guardas de arranque del registro (startup-configuration: «La siembra demo y el registro no pueden coexistir» y «El registro abierto
/// en producción exige ForwardedHeaders»). Se evalúan antes de cualquier migración o siembra y no imprimen ningún secreto: cada fallo
/// se comprueba contra una base vacía con las migraciones activadas, que debe seguir vacía.
/// </summary>
[Collection("Postgres")]
public class RegistrationStartupGuardsTests(PostgresFixture postgres)
{
    private const string RegistrationKey = "Registration:Enabled";
    private const string CodesKey = "Registration:InviteCodes";
    private const string DemoUsersKey = "Seed:DemoUsers";
    private const string DemoDataKey = "Seed:DemoData";
    private const string DemoPasswordKey = "Seed:DemoPassword";
    private const string ForwardedEnabledKey = "ForwardedHeaders:Enabled";
    private const string HopsKey = "ForwardedHeaders:Hops";
    private const string Code = "codigo-prueba-0123456789";
    private const string DemoPassword = "contrasena-demo-prueba";

    [Fact]
    public async Task RegistrationAndDemoUsersTogetherInProduction_FailBeforeAnyMigration_NamingBothKeysWithoutSecrets()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, migrate: true, production: true, (RegistrationKey, "true"), (CodesKey, Code), (ForwardedEnabledKey, "true"), (DemoUsersKey, "true"), (DemoPasswordKey, DemoPassword));

        var exception = factory.StartExpectingFailure();

        var messages = StartupFailure.AllMessages(exception);
        Assert.Contains(DemoUsersKey, messages, StringComparison.Ordinal);
        Assert.Contains(RegistrationKey, messages, StringComparison.Ordinal);
        Assert.Contains("contraseña pública", messages, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, messages, StringComparison.Ordinal);
        Assert.DoesNotContain(DemoPassword, messages, StringComparison.Ordinal);
        Assert.DoesNotContain(StartupFailure.Flatten(exception), inner => inner is PostgresException or NpgsqlException);
        await AssertNothingWasMigratedAsync(connectionString);
    }

    [Fact]
    public async Task RegistrationAndDemoDataTogetherInProduction_AlsoFailNamingTheSeedAndTheRegistrationKeys()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, migrate: true, production: true, (RegistrationKey, "true"), (CodesKey, Code), (ForwardedEnabledKey, "true"), (DemoUsersKey, "true"), (DemoDataKey, "true"), (DemoPasswordKey, DemoPassword));

        var exception = factory.StartExpectingFailure();

        Assert.Contains(DemoUsersKey, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
        Assert.Contains(RegistrationKey, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
        await AssertNothingWasMigratedAsync(connectionString);
    }

    [Fact]
    public async Task RegistrationAndDemoUsersTogetherInDevelopment_Start_BecauseTheNewCompanyIsIsolatedFromTheDemoOnes()
    {
        // ADR 0033: en Development coexisten (la compañía nueva queda aislada por company_id); en Production la regla se mantiene.
        using var factory = CreateFactory(postgres.ConnectionString, migrate: false, production: false, (RegistrationKey, "true"), (CodesKey, Code), (DemoUsersKey, "true"), (DemoDataKey, "true"), (DemoPasswordKey, DemoPassword));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task TheDevelopmentInviteCodeOutsideDevelopment_FailsBeforeAnyMigration_NamingTheKeyWithoutPrintingIt(string environment)
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
            builder.UseSetting(RegistrationKey, "true");
            builder.UseSetting(CodesKey, $"{Code},{DevelopmentInviteCode.Value}");
            builder.UseSetting(ForwardedEnabledKey, "true");
        });

        var exception = factory.StartExpectingFailure();

        var messages = StartupFailure.AllMessages(exception);
        Assert.Contains(CodesKey, messages, StringComparison.Ordinal);
        Assert.Contains("desarrollo", messages, StringComparison.Ordinal);
        Assert.DoesNotContain(DevelopmentInviteCode.Value, messages, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, messages, StringComparison.Ordinal);
        await AssertNothingWasMigratedAsync(connectionString);
    }

    [Fact]
    public async Task TheDevelopmentInviteCodeInDevelopment_Starts()
    {
        using var factory = CreateFactory(postgres.ConnectionString, migrate: false, production: false, (RegistrationKey, "true"), (CodesKey, DevelopmentInviteCode.Value));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OnlyTheSeed_Starts()
    {
        using var factory = CreateFactory(postgres.ConnectionString, migrate: false, (DemoUsersKey, "true"), (DemoPasswordKey, DemoPassword));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OnlyTheRegistration_Starts()
    {
        using var factory = CreateFactory(postgres.ConnectionString, migrate: false, (RegistrationKey, "true"), (CodesKey, Code));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task AnOpenRegistrationInProductionWithoutForwardedHeaders_FailsBeforeAnyMigration_NamingTheKeyWithoutTheCode(string? forwarded)
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, migrate: true, production: true, (RegistrationKey, "true"), (CodesKey, Code), (ForwardedEnabledKey, forwarded));

        var exception = factory.StartExpectingFailure();

        var messages = StartupFailure.AllMessages(exception);
        Assert.Contains(ForwardedEnabledKey, messages, StringComparison.Ordinal);
        Assert.Contains("X-Forwarded-For", messages, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, messages, StringComparison.Ordinal);
        Assert.DoesNotContain(StartupFailure.Flatten(exception), inner => inner is PostgresException or NpgsqlException);
        await AssertNothingWasMigratedAsync(connectionString);
    }

    [Fact]
    public async Task AnOpenRegistrationInProductionWithNoHops_FailsBeforeAnyMigration_NamingTheHopsKeyWithoutTheCode()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, migrate: true, production: true, (RegistrationKey, "true"), (CodesKey, Code), (ForwardedEnabledKey, "true"), (HopsKey, "0"));

        var exception = factory.StartExpectingFailure();

        var messages = StartupFailure.AllMessages(exception);
        Assert.Contains(HopsKey, messages, StringComparison.Ordinal);
        Assert.Contains("X-Forwarded-For", messages, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, messages, StringComparison.Ordinal);
        Assert.DoesNotContain(StartupFailure.Flatten(exception), inner => inner is PostgresException or NpgsqlException);
        await AssertNothingWasMigratedAsync(connectionString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("2")]
    public async Task AnOpenRegistrationInProductionWithForwardedHeaders_Starts(string? hops)
    {
        // Base vacía y migrada al arrancar: sin usuarios de demostración (la compartida los tiene de otras pruebas).
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = CreateFactory(connectionString, migrate: true, production: true, (RegistrationKey, "true"), (CodesKey, Code), (ForwardedEnabledKey, "true"), (HopsKey, hops));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnOpenRegistrationOutsideProduction_StartsWithoutForwardedHeaders()
    {
        using var factory = CreateFactory(postgres.ConnectionString, migrate: false, production: false, (RegistrationKey, "true"), (CodesKey, Code));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AClosedRegistrationInProduction_StartsWithoutForwardedHeaders()
    {
        using var factory = CreateFactory(postgres.ConnectionString, migrate: false, production: true);

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void TheDemoDataKeepsRequiringTheDemoUsers()
    {
        using var factory = CreateFactory("Host=db.invalid;Database=timetracker;Username=timetracker", migrate: false, (DemoDataKey, "true"));

        var exception = factory.StartExpectingFailure();

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, DemoDataKey);
        Assert.Contains(DemoUsersKey, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, bool migrate, params (string Key, string? Value)[] settings)
        => CreateFactory(connectionString, migrate, production: false, settings);

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, bool migrate, bool production, params (string Key, string? Value)[] settings)
        => new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", migrate ? "true" : "false");
            if (production)
            {
                builder.UseEnvironment("Production");
            }

            foreach (var (key, value) in settings)
            {
                if (value is not null)
                {
                    builder.UseSetting(key, value);
                }
            }
        });

    // Una base vacía con las migraciones activadas queda sin una sola tabla si el arranque se detuvo antes de migrar.
    private static async Task AssertNothingWasMigratedAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'", connection);
        Assert.Equal(0L, await command.ExecuteScalarAsync(CancellationToken.None));
    }
}
