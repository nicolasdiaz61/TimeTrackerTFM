using System.Net;

using Microsoft.AspNetCore.Hosting;

using TimeTracker.Api.Authentication;

namespace TimeTracker.Api.IntegrationTests.Startup;

public class JwtStartupTests
{
    private const string SigningKeyKey = "Jwt:SigningKey";
    private const string ShortSecret = "corta-secreta-prueba";

    [Fact]
    public void Startup_WithoutSigningKey_FailsNamingKey()
        => DatabaseStartupTests.AssertStartupFailsNamingKey(DatabaseStartupTests.StartWith(SigningKeyKey, null), SigningKeyKey);

    [Fact]
    public void Startup_WithBlankSigningKey_Fails()
        => DatabaseStartupTests.AssertStartupFailsNamingKey(DatabaseStartupTests.StartWith(SigningKeyKey, "   "), SigningKeyKey);

    [Fact]
    public void Startup_With31Bytes_FailsNamingKey()
        => DatabaseStartupTests.AssertStartupFailsNamingKey(DatabaseStartupTests.StartWith(SigningKeyKey, new string('a', 31)), SigningKeyKey);

    [Fact]
    public void Startup_DoesNotPrintTheSigningKey()
    {
        var exception = DatabaseStartupTests.StartWith(SigningKeyKey, ShortSecret);

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, SigningKeyKey);
        Assert.DoesNotContain(ShortSecret, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345")]
    [InlineData("ñññññññññññññññ" + "ñ")]
    public async Task Startup_With32AsciiOr16EnyeChars_StartsAndHealthIsOk(string signingKey)
    {
        // 32 caracteres ASCII y 16 'ñ' miden 32 bytes en UTF-8: el límite se mide en bytes, no en caracteres.
        using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting(SigningKeyKey, signingKey));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startup_InProduction_WithValidConfiguration_StartsAndHealthIsOk()
    {
        // Sin identidad provisional la API ya arranca fuera de Development con la configuración completa (SC-14).
        using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Production_WithoutSigningKey_FailsNamingKey()
    {
        // Los valores ficticios de DocumentGeneration solo existen bajo la herramienta de exportación (SA-27).
        using var factory = new ApiFactory().WithWebHostBuilder(
            builder => builder.UseEnvironment("Production").UseSetting(SigningKeyKey, null));

        var exception = factory.StartExpectingFailure();

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, SigningKeyKey);
    }

    [Fact]
    public void Production_WithTheDevelopmentSigningKey_FailsNamingKeyWithoutPrintingIt()
    {
        // La clave de desarrollo está versionada en appsettings.Development.json: fuera de Development es pública y no puede firmar tokens.
        using var factory = new ApiFactory().WithWebHostBuilder(
            builder => builder.UseEnvironment("Production").UseSetting(SigningKeyKey, DevelopmentSigningKey.Value));

        var exception = factory.StartExpectingFailure();

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, SigningKeyKey);
        Assert.DoesNotContain(DevelopmentSigningKey.Value, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_WithTheDevelopmentSigningKey_Starts()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(
            builder => builder.UseEnvironment("Development").UseSetting(SigningKeyKey, DevelopmentSigningKey.Value));

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startup_InDevelopment_WithoutIdentityKey_Starts()
    {
        // La fábrica no aporta ninguna clave Identity: el arranque depende solo de Cors, ConnectionStrings y Jwt (SC-15).
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
