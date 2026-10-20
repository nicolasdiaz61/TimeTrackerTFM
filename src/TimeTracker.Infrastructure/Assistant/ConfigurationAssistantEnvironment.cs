using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Valores <c>Ai:*</c> del entorno tal como los consume Application. Del endpoint de Ollama solo se expone si existe.</summary>
internal sealed class ConfigurationAssistantEnvironment(IOptions<AiOptions> options) : IAssistantEnvironment
{
    public AssistantEnvironmentDefaults Current
    {
        get
        {
            var env = options.Value;
            return new AssistantEnvironmentDefaults(
                env.ResolvedProvider?.ToAssistantProvider() ?? AssistantProvider.None,
                env.Model,
                env.TimeoutSeconds,
                env.MaxOutputTokens,
                env.ResolvedEffort,
                string.IsNullOrWhiteSpace(env.ApiKey) ? null : new AssistantCredential(env.ApiKey),
                !string.IsNullOrWhiteSpace(env.Endpoint),
                env.AllowedProviders);
        }
    }
}
