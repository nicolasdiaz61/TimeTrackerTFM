using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TimeTracker.Api.Configuration;

namespace TimeTracker.Api.IntegrationTests.Startup;

public class DatabaseStartupTests
{
    private const string ConnectionStringKey = "ConnectionStrings:Default";
    private const string ApplyMigrationsKey = "Database:ApplyMigrationsOnStartup";
    private const string FakePassword = "secreto-prueba";
    private const string MalformedConnectionString = "Host=db.invalid;Password=" + FakePassword + ";;=;Puerto-roto";

    [Fact]
    public void Startup_WithoutConnectionString_FailsNamingKey()
        => AssertStartupFailsNamingKey(StartWith(ConnectionStringKey, null), ConnectionStringKey);

    [Fact]
    public void Startup_WithBlankConnectionString_Fails()
        => AssertStartupFailsNamingKey(StartWith(ConnectionStringKey, "   "), ConnectionStringKey);

    [Fact]
    public void Startup_WithMalformedConnectionString_Fails()
        => AssertStartupFailsNamingKey(StartWith(ConnectionStringKey, MalformedConnectionString), ConnectionStringKey);

    [Fact]
    public void Startup_WithMalformedConnectionString_DoesNotPrintTheValue()
    {
        var exception = StartWith(ConnectionStringKey, MalformedConnectionString);

        Assert.NotNull(exception);
        var messages = StartupFailure.AllMessages(exception);
        Assert.DoesNotContain(FakePassword, messages);
        Assert.DoesNotContain("Puerto-roto", messages);
        Assert.DoesNotContain("Format of the initialization string", messages);
    }

    [Fact]
    public void DatabaseOptions_WithoutSetting_IsFalse()
    {
        using var factory = new ApiFactory();

        var options = factory.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        Assert.False(options.ApplyMigrationsOnStartup);
    }

    [Fact]
    public void Startup_WithNonBooleanFlag_FailsNamingKey()
    {
        var exception = StartWith(ApplyMigrationsKey, "quizas");

        Assert.NotNull(exception);
        Assert.Contains(ApplyMigrationsKey, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    internal static Exception? StartWith(string key, string? value)
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting(key, value));
        return Record.Exception(() => factory.CreateClient());
    }

    internal static void AssertStartupFailsNamingKey(Exception? exception, string key)
        => Assert.Contains(StartupFailure.Validations(exception), validation => validation.Message.Contains(key, StringComparison.Ordinal));
}
