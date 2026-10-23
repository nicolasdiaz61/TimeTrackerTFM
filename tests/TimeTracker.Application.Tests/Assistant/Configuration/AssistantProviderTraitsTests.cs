using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class AssistantProviderTraitsTests
{
    [Theory]
    [InlineData(AssistantProvider.Anthropic)]
    [InlineData(AssistantProvider.OpenAI)]
    [InlineData(AssistantProvider.Gemini)]
    [InlineData(AssistantProvider.Groq)]
    public void CloudProviders_RequireACredentialSupportEffortAndNeedNoEnvironmentEndpoint(AssistantProvider provider)
        => Assert.Equal(new AssistantProviderTraits(true, true, false), AssistantProviderTraits.For(provider));

    [Fact]
    public void Ollama_NeedsNoCredentialButNeedsTheEnvironmentEndpoint()
        => Assert.Equal(new AssistantProviderTraits(false, true, true), AssistantProviderTraits.For(AssistantProvider.Ollama));

    [Fact]
    public void None_HasNoCapabilities()
        => Assert.Equal(new AssistantProviderTraits(false, false, false), AssistantProviderTraits.For(AssistantProvider.None));
}
