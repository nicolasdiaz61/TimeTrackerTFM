namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Registro guardado. Los campos anulables significan «usar el valor del entorno».</summary>
public sealed record AssistantSettings(
    AssistantProvider Provider,
    string? Model,
    int? TimeoutSeconds,
    int? MaxOutputTokens,
    AssistantEffort? Effort,
    EncryptedCredential? Credential,
    Guid UpdatedBy,
    DateTimeOffset UpdatedAt,
    string Version);
