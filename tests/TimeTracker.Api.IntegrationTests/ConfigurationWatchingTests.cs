using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TimeTracker.Api.IntegrationTests;

public class ConfigurationWatchingTests
{
    [Fact]
    public void Factory_DoesNotWatchConfigurationFiles()
    {
        // Cada host de prueba que vigila appsettings.json consume una instancia de inotify en Linux, cuyo límite por
        // usuario (128 en los ejecutores de CI) se agota con muchas fábricas vivas: el arranque falla con IOException
        // y las pruebas que esperan OptionsValidationException no la encuentran.
        using var factory = new ApiFactory();

        var configuration = Assert.IsAssignableFrom<IConfigurationRoot>(factory.Services.GetRequiredService<IConfiguration>());
        var fileProviders = configuration.Providers.OfType<FileConfigurationProvider>().ToList();

        Assert.NotEmpty(fileProviders);
        Assert.DoesNotContain(fileProviders, provider => provider.Source.ReloadOnChange);
    }
}
