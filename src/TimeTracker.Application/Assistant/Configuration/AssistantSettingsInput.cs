namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Entrada en texto tal como llega de la API, para validar aquí y devolver mensajes en español.</summary>
public sealed record AssistantSettingsInput(
    string? Provider,
    string? Model,
    int? TimeoutSeconds,
    int? MaxOutputTokens,
    string? Effort,
    string? Credential);
