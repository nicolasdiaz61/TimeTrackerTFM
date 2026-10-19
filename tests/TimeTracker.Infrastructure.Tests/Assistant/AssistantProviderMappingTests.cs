using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantProviderMappingTests
{
    [Fact]
    public void AiProviderAndAssistantProvider_ShareTheSameNames()
    {
        Assert.Equal(Enum.GetNames<AssistantProvider>().Order(), Enum.GetNames<AiProvider>().Order());
    }

    [Fact]
    public void AiProvider_KeepsItsExistingNumbersAndAddsTheNewOnesAtTheEnd()
    {
        Assert.Equal([0, 1, 2, 3, 4], new[] { AiProvider.None, AiProvider.Anthropic, AiProvider.Ollama, AiProvider.OpenAI, AiProvider.Gemini }.Select(p => (int)p));
    }
}
