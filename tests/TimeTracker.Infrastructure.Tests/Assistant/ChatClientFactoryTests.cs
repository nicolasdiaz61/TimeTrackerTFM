using Anthropic;

using Microsoft.Extensions.AI;

using OllamaSharp;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

// Ninguna prueba abre una conexión: construir un cliente no hace red.
public sealed class ChatClientFactoryTests
{
    private const string SecretKey = "clave-secreta-prueba";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("None")]
    public void Create_WithoutProvider_ReturnsNoClient(string? provider)
    {
        Assert.Null(ChatClientFactory.Create(new AiOptions { Provider = provider, Model = "modelo-ficticio", ApiKey = SecretKey }));
    }

    [Fact]
    public void Create_WithAnthropic_ReturnsTheAnthropicChatClientForTheConfiguredModel()
    {
        using var client = ChatClientFactory.Create(new AiOptions { Provider = "Anthropic", Model = "modelo-ficticio", ApiKey = SecretKey });

        Assert.NotNull(client);
        var metadata = client.GetService<ChatClientMetadata>();
        Assert.Equal("modelo-ficticio", metadata?.DefaultModelId);
        Assert.Contains("anthropic", metadata?.ProviderName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateAnthropicClient_UsesTheConfiguredKeyAndASingleRetry()
    {
        var client = ChatClientFactory.CreateAnthropicClient(new AiOptions { Provider = "Anthropic", Model = "m", ApiKey = SecretKey });

        Assert.IsType<AnthropicClient>(client);
        Assert.Equal(SecretKey, client.ApiKey);
        Assert.Equal(1, client.MaxRetries);
    }

    [Fact]
    public void Create_WithOllama_ReturnsAnOllamaClientForTheConfiguredEndpointAndModel()
    {
        using var client = ChatClientFactory.Create(new AiOptions { Provider = "ollama", Model = "modelo-ficticio", Endpoint = "http://localhost:11434/" });

        var ollama = Assert.IsType<OllamaApiClient>(client);
        Assert.Equal("modelo-ficticio", ollama.SelectedModel);
        Assert.Equal(new Uri("http://localhost:11434/"), ollama.Uri);
    }

    [Fact]
    public void Create_WithAnUnrecognisedProvider_ReturnsNoClient()
    {
        Assert.Null(ChatClientFactory.Create(new AiOptions { Provider = "1", Model = "m", ApiKey = SecretKey }));
    }
}
