using System.Net;

using Microsoft.AspNetCore.Hosting;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>Arranque con <c>Ai:*</c> (sin base de datos: la cadena de conexión apunta a un host inexistente y no se abre).</summary>
public class AiStartupTests
{
    private const string SecretKey = "clave-secreta-prueba";

    private static Exception? StartWith(params (string Key, string? Value)[] settings)
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
        return factory.StartExpectingFailure();
    }

    private static async Task AssertStartsAsync(params (string Key, string? Value)[] settings)
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public Task WithoutAiKeys_StartsAndHealthIsOk() => AssertStartsAsync();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("None")]
    public Task WithoutProvider_StartsEvenWithInvalidLimits(string provider)
        => AssertStartsAsync(("Ai:Provider", provider), ("Ai:MaxOutputTokens", "-5"), ("Ai:TimeoutSeconds", "999"));

    [Fact]
    public void Anthropic_WithoutApiKey_FailsNamingTheKey()
        => DatabaseStartupTests.AssertStartupFailsNamingKey(StartWith(("Ai:Provider", "Anthropic"), ("Ai:Model", "modelo-ficticio")), "Ai:ApiKey");

    [Theory]
    [InlineData("GitHubModels")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    public void UnknownOrNumericProvider_FailsNamingTheKey(string provider)
        => DatabaseStartupTests.AssertStartupFailsNamingKey(StartWith(("Ai:Provider", provider), ("Ai:Model", "m"), ("Ai:ApiKey", SecretKey)), "Ai:Provider");

    [Fact]
    public Task Ollama_WithValidConfiguration_StartsWithoutAServer()
        => AssertStartsAsync(("Ai:Provider", "Ollama"), ("Ai:Model", "modelo-ficticio"), ("Ai:Endpoint", "http://localhost:1/"));

    [Fact]
    public Task Anthropic_WithValidConfiguration_StartsWithoutContactingTheProvider()
        => AssertStartsAsync(("Ai:Provider", "Anthropic"), ("Ai:Model", "modelo-ficticio"), ("Ai:ApiKey", SecretKey));

    [Theory]
    [InlineData("Ai:MaxOutputTokens", "63")]
    [InlineData("Ai:MaxOutputTokens", "4097")]
    [InlineData("Ai:MaxToolIterations", "0")]
    [InlineData("Ai:MaxToolIterations", "11")]
    [InlineData("Ai:TimeoutSeconds", "4")]
    [InlineData("Ai:TimeoutSeconds", "56")]
    [InlineData("Ai:RequestsPerMinute", "0")]
    [InlineData("Ai:RequestsPerMinute", "61")]
    [InlineData("Ai:DailyRequestsPerUser", "0")]
    [InlineData("Ai:DailyRequestsPerUser", "1001")]
    public void InvalidLimit_FailsNamingTheKeyWithoutPrintingTheApiKey(string key, string value)
    {
        var exception = StartWith(("Ai:Provider", "Anthropic"), ("Ai:Model", "m"), ("Ai:ApiKey", SecretKey), (key, value));

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, key);
        Assert.DoesNotContain(SecretKey, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ai:MaxOutputTokens", "64")]
    [InlineData("Ai:MaxOutputTokens", "4096")]
    [InlineData("Ai:MaxToolIterations", "1")]
    [InlineData("Ai:MaxToolIterations", "10")]
    [InlineData("Ai:TimeoutSeconds", "5")]
    [InlineData("Ai:TimeoutSeconds", "55")]
    [InlineData("Ai:RequestsPerMinute", "1")]
    [InlineData("Ai:RequestsPerMinute", "60")]
    [InlineData("Ai:DailyRequestsPerUser", "1")]
    [InlineData("Ai:DailyRequestsPerUser", "1000")]
    public Task LimitAtTheEdge_Starts(string key, string value)
        => AssertStartsAsync(("Ai:Provider", "Ollama"), ("Ai:Model", "m"), ("Ai:Endpoint", "http://localhost:1/"), (key, value));

    [Fact]
    public void Failure_NeverPrintsTheApiKeyInAnyException()
    {
        var exception = StartWith(("Ai:Provider", "Anthropic"), ("Ai:ApiKey", SecretKey), ("Ai:MaxOutputTokens", "1"));

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, "Ai:MaxOutputTokens");
        Assert.DoesNotContain(SecretKey, StartupFailure.Describe(exception), StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutProvider_MissingSigningKeyStillFails()
        => DatabaseStartupTests.AssertStartupFailsNamingKey(StartWith(("Jwt:SigningKey", null)), "Jwt:SigningKey");

    [Fact]
    public void AppSettingsFiles_DoNotDefineAiKeys()
    {
        var apiDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TimeTracker.Api");
        var files = Directory.GetFiles(Path.GetFullPath(apiDirectory), "appsettings*.json");

        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.DoesNotContain("\"Ai\"", File.ReadAllText(file), StringComparison.Ordinal));
    }
}
