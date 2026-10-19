using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TimeTracker.Api.IntegrationTests.Support;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>Arranque con y sin clave maestra: una línea informativa con el estado y sin valores (DD-7).</summary>
public class ProtectionStatusLogTests
{
    private static readonly string MasterKey = Convert.ToBase64String(new byte[32]);

    private static string[] StartAndCapture(params (string Key, string? Value)[] settings)
    {
        var provider = new CapturingLoggerProvider();
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(provider));
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
        _ = factory.Services;
        return [.. provider.Messages.Where(message => message.Contains("protección de claves", StringComparison.Ordinal))];
    }

    [Fact]
    public void WithAValidActiveMasterKey_LogsAvailableWithoutTheKey()
    {
        var lines = StartAndCapture(("Ai:MasterKeys:1", MasterKey), ("Ai:ActiveMasterKeyVersion", "1"));

        var line = Assert.Single(lines);
        Assert.DoesNotContain("no disponible", line, StringComparison.Ordinal);
        Assert.DoesNotContain(MasterKey, line, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutMasterKeys_LogsUnavailableAndStarts()
    {
        var line = Assert.Single(StartAndCapture());

        Assert.Contains("no disponible", line, StringComparison.Ordinal);
    }

    [Fact]
    public void WithAnInvalidMasterKey_LogsUnavailableWithoutPrintingIt()
    {
        var line = Assert.Single(StartAndCapture(("Ai:MasterKeys:1", "esto-no-es-base64-secreto"), ("Ai:ActiveMasterKeyVersion", "1")));

        Assert.Contains("no disponible", line, StringComparison.Ordinal);
        Assert.DoesNotContain("esto-no-es-base64-secreto", line, StringComparison.Ordinal);
    }
}
