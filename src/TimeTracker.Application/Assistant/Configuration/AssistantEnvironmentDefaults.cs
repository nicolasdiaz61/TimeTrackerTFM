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
    string? AllowedProviders)
{
    /// <summary>
    /// Lo que del entorno puede usar una compañía (DD-13): la compañía de plataforma lo ve entero; las demás no heredan el modelo,
    /// el esfuerzo ni la clave <c>Ai:ApiKey</c> (son del proveedor de plataforma). El tiempo máximo, los tokens y la lista de
    /// proveedores permitidos son valores de plataforma no secretos y se conservan.
    /// </summary>
    public AssistantEnvironmentDefaults ForCompany(bool platformDefaults)
        => platformDefaults ? this : this with { Model = null, Effort = null, Credential = null };
}
