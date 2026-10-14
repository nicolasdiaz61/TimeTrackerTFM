using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.Seeding;

namespace TimeTracker.Api.IntegrationTests.Startup;

[Collection("Postgres")]
public class SeedStartupTests(PostgresFixture postgres)
{
    private const string DemoUsersKey = "Seed:DemoUsers";
    private const string DemoPasswordKey = "Seed:DemoPassword";

    [Fact]
    public async Task SeedOptions_WithoutSetting_DemoUsersIsFalse_AndStartsWithoutPassword()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(factory.Services.GetRequiredService<IOptions<SeedOptions>>().Value.DemoUsers);
    }

    [Fact]
    public void Startup_WithNonBooleanDemoUsers_FailsNamingKey()
    {
        var exception = DatabaseStartupTests.StartWith(DemoUsersKey, "quizas");

        Assert.NotNull(exception);
        Assert.Contains(DemoUsersKey, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_DemoUsersTrueWithoutPassword_FailsNamingKey()
        => DatabaseStartupTests.AssertStartupFailsNamingKey(Start(demoUsers: "true", password: null), DemoPasswordKey);

    [Fact]
    public void Startup_With11CharPassword_Fails()
        => DatabaseStartupTests.AssertStartupFailsNamingKey(Start(demoUsers: "true", password: new string('d', 11)), DemoPasswordKey);

    [Fact]
    public void Startup_DoesNotPrintDemoPassword()
    {
        const string shortPassword = "corta-demo";

        var exception = Start(demoUsers: "true", password: shortPassword);

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, DemoPasswordKey);
        Assert.DoesNotContain(shortPassword, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_With12CharPassword_StartsWithMigratedDatabase()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", postgres.ConnectionString);
            builder.UseSetting(DemoUsersKey, "true");
            builder.UseSetting(DemoPasswordKey, new string('d', 12));
        });

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startup_DemoUsersFalse_IgnoresShortPassword()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(DemoUsersKey, "false");
            builder.UseSetting(DemoPasswordKey, "abc");
        });

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Exception? Start(string demoUsers, string? password)
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(DemoUsersKey, demoUsers);
            builder.UseSetting(DemoPasswordKey, password);
        });
        return factory.StartExpectingFailure();
    }
}
