using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TimeTracker.Api.Configuration;

namespace TimeTracker.Api.IntegrationTests;

public class StartupConfigurationTests
{
    private const string AllowedOriginsKey = "Cors:AllowedOrigins";

    [Fact]
    public void Startup_WithoutAllowedOrigins_FailsNamingKey()
    {
        using var factory = new ApiFactory()
            .WithWebHostBuilder(builder => builder.UseSetting($"{AllowedOriginsKey}:0", null));

        var exception = Record.Exception(() => factory.CreateClient());

        var validation = Assert.Single(StartupFailure.Validations(exception));
        Assert.Contains(AllowedOriginsKey, validation.Message);
    }

    [Fact]
    public void Startup_WithValidOrigin_BindsTypedOptions()
    {
        using var factory = new ApiFactory();

        var options = factory.Services.GetRequiredService<IOptions<ApiCorsOptions>>().Value;

        Assert.Equal([ApiFactory.ValidOrigin], options.AllowedOrigins);
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("app.example.test/ruta")]
    [InlineData(" ")]
    [InlineData("ftp://app.example.test")]
    [InlineData("https://a.test/ruta")]
    [InlineData("https://a.test/")]
    public void Startup_WithInvalidAllowedOrigins_Fails(string origin)
    {
        using var factory = new ApiFactory()
            .WithWebHostBuilder(builder => builder.UseSetting($"{AllowedOriginsKey}:0", origin));

        var exception = Record.Exception(() => factory.CreateClient());

        var validation = Assert.Single(StartupFailure.Validations(exception));
        Assert.Contains(AllowedOriginsKey, validation.Message);
        if (!string.IsNullOrWhiteSpace(origin))
        {
            // Los mensajes nombran la clave y la razón, pero nunca repiten el valor recibido.
            Assert.DoesNotContain(origin, validation.Message);
        }
    }
}
