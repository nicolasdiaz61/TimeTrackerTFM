using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using TimeTracker.Api.Authentication;
using TimeTracker.Api.Configuration;
using TimeTracker.Api.Registration;
using TimeTracker.Api.Seeding;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>
/// La configuración de desarrollo versionada en <c>appsettings.Development.json</c> (ADR 0032) debe bastar para arrancar
/// sin variables de entorno: se comprueba con los mismos validadores que corren al arrancar.
/// </summary>
public class DevelopmentConfigurationTests
{
    private static readonly string ApiDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TimeTracker.Api"));

    private static IConfigurationRoot Load()
        => new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ApiDirectory, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(ApiDirectory, "appsettings.Development.json"), optional: false)
            .Build();

    [Fact]
    public void SigningKey_IsTheSharedConstant_AndIsValid()
    {
        var jwt = Load().GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

        Assert.Equal(DevelopmentSigningKey.Value, jwt.SigningKey);
        Assert.True(Encoding.UTF8.GetByteCount(DevelopmentSigningKey.Value) >= JwtOptionsValidator.MinimumSigningKeyBytes);
        Assert.True(new JwtOptionsValidator(new FakeEnvironment("Development")).Validate(null, jwt).Succeeded);
    }

    [Fact]
    public void Cors_OnlyAllowsTheLocalWeb_AndIsValid()
    {
        var cors = Load().GetSection("Cors").Get<ApiCorsOptions>()!;

        Assert.Equal(["http://localhost:5172"], cors.AllowedOrigins);
        Assert.True(new ApiCorsOptionsValidator().Validate(null, cors).Succeeded);
    }

    [Fact]
    public void Seed_EnablesDemoUsersAndData_WithAValidPassword()
    {
        var seed = Load().GetSection("Seed").Get<SeedOptions>()!;

        Assert.True(seed.DemoUsers);
        Assert.True(seed.DemoData);
        Assert.NotEqual("Demo-Fichajes-2026", seed.DemoPassword);
        Assert.True(new SeedOptionsValidator().Validate(null, seed).Succeeded);
    }

    [Fact]
    public void Database_AppliesMigrations_AndPointsToLocalPostgres()
    {
        var configuration = Load();

        Assert.True(configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"));
        var connection = configuration.GetConnectionString("Default")!;
        Assert.Contains("Host=localhost;Port=5432", connection, StringComparison.Ordinal);
    }

    [Fact]
    public void Registration_IsOpenWithTheSharedDevelopmentInviteCode_AndIsValidTogetherWithTheDemoSeed()
    {
        var configuration = Load();
        var registration = configuration.GetSection(RegistrationOptions.SectionName).Get<RegistrationOptions>()!;
        var seed = configuration.GetSection("Seed").Get<SeedOptions>()!;

        Assert.True(registration.Enabled);
        Assert.Equal([DevelopmentInviteCode.Value], registration.ParseInviteCodes());
        Assert.True(DevelopmentInviteCode.Value.Length >= RegistrationOptionsValidator.MinimumCodeLength);
        Assert.True(seed.DemoUsers);
        Assert.True(Validator(configuration, seed, "Development").Validate(null, registration).Succeeded);
    }

    [Fact]
    public void RegistrationTogetherWithTheDemoSeed_StillFailsInProduction()
    {
        var configuration = Load();
        var registration = configuration.GetSection(RegistrationOptions.SectionName).Get<RegistrationOptions>()!;
        var seed = configuration.GetSection("Seed").Get<SeedOptions>()!;

        var result = Validator(configuration, seed, "Production").Validate(null, registration);

        Assert.True(result.Failed);
        Assert.Contains("Seed:DemoUsers", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void BaseAppSettings_KeepTheRegistrationClosed()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(ApiDirectory, "appsettings.json"), optional: false).Build();

        Assert.False(configuration.GetValue<bool>("Registration:Enabled"));
        Assert.Null(configuration["Registration:InviteCodes"]);
    }

    private static RegistrationOptionsValidator Validator(IConfiguration configuration, SeedOptions seed, string environment)
        => new(configuration, Options.Create(seed), new FakeEnvironment(environment));

    private sealed class FakeEnvironment(string name) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
