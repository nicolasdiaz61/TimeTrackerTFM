using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

internal static class AiProviderMapping
{
    /// <summary>Correspondencia por nombre; <c>AssistantProviderMappingTests</c> garantiza que ambos catálogos coinciden.</summary>
    public static AssistantProvider ToAssistantProvider(this AiProvider provider) => Enum.Parse<AssistantProvider>(provider.ToString());
}
