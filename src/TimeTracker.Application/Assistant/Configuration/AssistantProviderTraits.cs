namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Capacidades por proveedor (no por modelo: el servidor no conoce las capacidades de un modelo en texto libre).</summary>
public sealed record AssistantProviderTraits(bool RequiresCredential, bool SupportsEffort, bool RequiresEnvironmentEndpoint)
{
    public static AssistantProviderTraits For(AssistantProvider provider) => provider switch
    {
        AssistantProvider.Anthropic or AssistantProvider.OpenAI or AssistantProvider.Gemini or AssistantProvider.Groq => new(true, true, false),
        AssistantProvider.Ollama => new(false, true, true),
        _ => new(false, false, false),
    };
}
