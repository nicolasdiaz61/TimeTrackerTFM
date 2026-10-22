namespace TimeTracker.Application.Assistant.Configuration;

public enum SettingSource
{
    Env,
    Stored,
    None,

    /// <summary>Valor por defecto de una compañía sin configuración propia: el asistente está deshabilitado (no viene del entorno).</summary>
    Default,
}

public sealed record EffectiveSources(
    SettingSource Provider,
    SettingSource Model,
    SettingSource TimeoutSeconds,
    SettingSource MaxOutputTokens,
    SettingSource Effort,
    SettingSource Credential);

public sealed record EffectiveAssistantSettings(
    AssistantProvider Provider,
    string? Model,
    int TimeoutSeconds,
    int MaxOutputTokens,
    AssistantEffort? Effort,
    bool CredentialConfigured,
    EffectiveSources Sources);

/// <summary>
/// Fusiona la fila guardada sobre el entorno, valor a valor. No descifra: recibe si la clave guardada es utilizable.
/// Los valores del entorno que identifican al proveedor de plataforma (proveedor, modelo, esfuerzo y la clave <c>Ai:ApiKey</c>)
/// solo valen para la compañía con <c>platformDefaults</c> (la demo, DD-13); el tiempo máximo y los tokens son valores no
/// secretos de plataforma y los hereda cualquier compañía.
/// </summary>
public static class AssistantSettingsResolver
{
    public static EffectiveAssistantSettings Resolve(AssistantEnvironmentDefaults environment, AssistantSettings? stored, bool storedCredentialUsable, bool platformDefaults)
    {
        var env = environment.ForCompany(platformDefaults);
        var provider = stored?.Provider ?? (platformDefaults ? env.Provider : AssistantProvider.None);
        var credentialSource = ResolveCredentialSource(provider, env, stored, storedCredentialUsable);

        return new EffectiveAssistantSettings(
            provider,
            stored?.Model ?? env.Model,
            stored?.TimeoutSeconds ?? env.TimeoutSeconds,
            stored?.MaxOutputTokens ?? env.MaxOutputTokens,
            stored?.Effort ?? env.Effort,
            credentialSource != SettingSource.None,
            new EffectiveSources(
                stored is not null ? SettingSource.Stored : platformDefaults ? SettingSource.Env : SettingSource.Default,
                SourceOf(stored?.Model, platformDefaults),
                SourceOf(stored?.TimeoutSeconds, platformInherits: true),
                SourceOf(stored?.MaxOutputTokens, platformInherits: true),
                SourceOf(stored?.Effort, platformDefaults),
                credentialSource));
    }

    private static SettingSource SourceOf(object? storedValue, bool platformInherits)
        => storedValue is not null ? SettingSource.Stored : platformInherits ? SettingSource.Env : SettingSource.Default;

    private static SettingSource ResolveCredentialSource(AssistantProvider provider, AssistantEnvironmentDefaults environment, AssistantSettings? stored, bool storedCredentialUsable)
    {
        if (provider == AssistantProvider.None)
        {
            return SettingSource.None;
        }

        if (stored is { Credential: not null } && storedCredentialUsable)
        {
            return SettingSource.Stored;
        }

        return UsableCredential.FromEnvironment(environment, provider) is not null ? SettingSource.Env : SettingSource.None;
    }
}
