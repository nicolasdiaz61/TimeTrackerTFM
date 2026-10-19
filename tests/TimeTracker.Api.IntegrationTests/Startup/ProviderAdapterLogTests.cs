using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TimeTracker.Api.IntegrationTests.Support;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>Un proveedor del catálogo sin adaptador todavía deja el asistente deshabilitado y el arranque lo dice sin valores.</summary>
public class ProviderAdapterLogTests
{
    private const string SecretKey = "clave-secreta-prueba";

    private static string[] StartAndCapture(string provider)
    {
        var capture = new CapturingLoggerProvider();
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(capture));
            builder.UseSetting("Ai:Provider", provider);
            builder.UseSetting("Ai:Model", "modelo-ficticio");
            builder.UseSetting("Ai:ApiKey", SecretKey);
        });
        _ = factory.Services;
        return [.. capture.Messages.Where(message => message.Contains("adaptador", StringComparison.Ordinal))];
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("Gemini")]
    public void ProviderWithoutAdapter_LogsItAsDisabledWithoutValues(string provider)
    {
        var line = Assert.Single(StartAndCapture(provider));

        Assert.Contains($"el proveedor {provider} aún no tiene adaptador; el asistente queda deshabilitado", line, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretKey, line, StringComparison.Ordinal);
        Assert.DoesNotContain("modelo-ficticio", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderWithAdapter_LogsNothing() => Assert.Empty(StartAndCapture("Anthropic"));
}
