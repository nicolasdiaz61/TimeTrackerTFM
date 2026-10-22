using System.Net;

using Microsoft.AspNetCore.Hosting;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>Arranque con las claves de tope por compañía (assistant-configuration, «Topes del asistente por compañía»); sin base de datos.</summary>
public class AiCompanyLimitsStartupTests
{
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

    [Theory]
    [InlineData("Ai:RequestsPerMinutePerCompany", "0")]
    [InlineData("Ai:RequestsPerMinutePerCompany", "601")]
    [InlineData("Ai:DailyRequestsPerCompany", "0")]
    [InlineData("Ai:DailyRequestsPerCompany", "100001")]
    [InlineData("Ai:MaxLoadedCompanies", "0")]
    [InlineData("Ai:MaxLoadedCompanies", "1001")]
    [InlineData("Ai:RuntimeIdleMinutes", "0")]
    [InlineData("Ai:RuntimeIdleMinutes", "1441")]
    public void AValueOutOfRange_FailsNamingTheKey_EvenWithTheAssistantDisabled(string key, string value)
    {
        var exception = StartWith((key, value));

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, key);
        Assert.DoesNotContain($"'{value}'", StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ai:RequestsPerMinutePerCompany", "1")]
    [InlineData("Ai:RequestsPerMinutePerCompany", "600")]
    [InlineData("Ai:DailyRequestsPerCompany", "1")]
    [InlineData("Ai:DailyRequestsPerCompany", "100000")]
    [InlineData("Ai:MaxLoadedCompanies", "1")]
    [InlineData("Ai:MaxLoadedCompanies", "1000")]
    [InlineData("Ai:RuntimeIdleMinutes", "1")]
    [InlineData("Ai:RuntimeIdleMinutes", "1440")]
    public Task AValueAtTheEdge_Starts(string key, string value) => AssertStartsAsync((key, value));

    [Theory]
    [InlineData("Ai:RequestsPerMinutePerCompany")]
    [InlineData("Ai:DailyRequestsPerCompany")]
    [InlineData("Ai:MaxLoadedCompanies")]
    [InlineData("Ai:RuntimeIdleMinutes")]
    public void ANonNumericValue_FailsNamingTheKeyWithoutPrintingTheValue(string key)
    {
        var exception = StartWith((key, "muchas"));

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, key);
        Assert.DoesNotContain("muchas", StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettingsFiles_DoNotDefineTheCompanyLimits()
    {
        var apiDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TimeTracker.Api");
        var files = Directory.GetFiles(Path.GetFullPath(apiDirectory), "appsettings*.json");

        Assert.NotEmpty(files);
        Assert.All(files, file =>
        {
            var text = File.ReadAllText(file);
            foreach (var key in new[] { "RequestsPerMinutePerCompany", "DailyRequestsPerCompany", "MaxLoadedCompanies", "RuntimeIdleMinutes" })
            {
                Assert.DoesNotContain(key, text, StringComparison.Ordinal);
            }
        });
    }
}
