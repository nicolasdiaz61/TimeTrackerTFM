using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class ConfigurationAssistantEnvironmentTests
{
    private static AssistantEnvironmentDefaults Read(AiOptions options)
        => new ConfigurationAssistantEnvironment(Options.Create(options)).Current;

    [Fact]
    public void Current_MapsEveryEnvironmentValue()
    {
        var defaults = Read(new AiOptions
        {
            Provider = "gemini",
            Model = "modelo-env",
            ApiKey = "clave-env-123456",
            Endpoint = "http://localhost:11434/",
            Effort = "High",
            AllowedProviders = "Gemini, Ollama",
            TimeoutSeconds = 31,
            MaxOutputTokens = 900,
        });

        Assert.Equal(AssistantProvider.Gemini, defaults.Provider);
        Assert.Equal("modelo-env", defaults.Model);
        Assert.Equal(31, defaults.TimeoutSeconds);
        Assert.Equal(900, defaults.MaxOutputTokens);
        Assert.Equal(AssistantEffort.High, defaults.Effort);
        Assert.Equal("clave-env-123456", defaults.Credential?.Value);
        Assert.True(defaults.HasEndpoint);
        Assert.Equal("Gemini, Ollama", defaults.AllowedProviders);
    }

    [Fact]
    public void Current_WithoutOptionalValues_ReportsThemAsAbsent()
    {
        var defaults = Read(new AiOptions { ApiKey = "  ", Endpoint = " " });

        Assert.Equal(AssistantProvider.None, defaults.Provider);
        Assert.Null(defaults.Model);
        Assert.Null(defaults.Effort);
        Assert.Null(defaults.Credential);
        Assert.False(defaults.HasEndpoint);
        Assert.Null(defaults.AllowedProviders);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("GitHubModels")]
    [InlineData("None")]
    [InlineData("")]
    public void Current_WithAnUnrecognisedOrDisabledProvider_IsNone(string provider)
    {
        Assert.Equal(AssistantProvider.None, Read(new AiOptions { Provider = provider }).Provider);
    }

    [Fact]
    public void Current_WithAnUnknownEffort_IsAbsent()
    {
        Assert.Null(Read(new AiOptions { Effort = "extreme" }).Effort);
    }
}
