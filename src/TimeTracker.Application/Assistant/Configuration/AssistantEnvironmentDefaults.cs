namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Valores <c>Ai:*</c> del entorno. El endpoint de Ollama nunca sale de aquí: solo se sabe si existe.</summary>
public sealed record AssistantEnvironmentDefaults(
    AssistantProvider Provider,
    string? Model,
    int TimeoutSeconds,
    int MaxOutputTokens,
    AssistantEffort? Effort,
    AssistantCredential? Credential,
    bool HasEndpoint,
    string? AllowedProviders);
