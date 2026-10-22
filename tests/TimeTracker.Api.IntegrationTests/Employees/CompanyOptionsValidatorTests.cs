using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TimeTracker.Api.Companies;
using TimeTracker.Api.IntegrationTests.Startup;
using TimeTracker.Application.Companies;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// Tope de personas por compañía (startup-configuration, «Tope de personas por compañía configurado y validado»). Las opciones y su
/// validador viven en <c>TimeTracker.Api</c>, por eso se prueban aquí.
/// </summary>
public class CompanyOptionsValidatorTests
{
    private const string Key = "Company:MaxEmployees";

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFactory(string? value)
        => new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting(Key, value));

    [Fact]
    public async Task WithoutTheKey_TheCapIs50_AndTheApplicationPortExposesIt()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50, factory.Services.GetRequiredService<IOptions<CompanyOptions>>().Value.MaxEmployees);
        Assert.Equal(50, factory.Services.GetRequiredService<ICompanyLimits>().MaxEmployees);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1000", 1000)]
    [InlineData("3", 3)]
    public async Task AValueInRange_Starts_AndIsExposedThroughThePort(string value, int expected)
    {
        using var factory = CreateFactory(value);

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, factory.Services.GetRequiredService<ICompanyLimits>().MaxEmployees);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1001")]
    [InlineData("-5")]
    public void AValueOutOfRange_FailsNamingTheKeyWithoutPrintingTheValue(string value)
    {
        using var factory = CreateFactory(value);

        var exception = factory.StartExpectingFailure();

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, Key);
        Assert.DoesNotContain($"'{value}'", StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Fact]
    public void ANonNumericValue_FailsNamingTheKeyWithoutPrintingTheValue()
    {
        using var factory = CreateFactory("muchos");

        var exception = factory.StartExpectingFailure();

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, Key);
        Assert.DoesNotContain("muchos", StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettingsFiles_DoNotDefineTheCap()
    {
        var apiDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TimeTracker.Api");
        var files = Directory.GetFiles(Path.GetFullPath(apiDirectory), "appsettings*.json");

        Assert.NotEmpty(files);
        Assert.All(files, file =>
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("MaxEmployees", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Company\"", text, StringComparison.Ordinal);
        });
    }
}
