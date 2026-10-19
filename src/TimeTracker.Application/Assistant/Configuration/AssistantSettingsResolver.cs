namespace TimeTracker.Application.Assistant.Configuration;

public enum SettingSource
{
    Env,
    Stored,
    None,
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

/// <summary>Fusiona la fila guardada sobre el entorno, valor a valor. No descifra: recibe si la clave guardada es utilizable.</summary>
public static class AssistantSettingsResolver
{
    public static EffectiveAssistantSettings Resolve(AssistantEnvironmentDefaults environment, AssistantSettings? stored, bool storedCredentialUsable)
    {
        var provider = stored?.Provider ?? environment.Provider;
        var credentialSource = ResolveCredentialSource(provider, environment, stored, storedCredentialUsable);

        return new EffectiveAssistantSettings(
            provider,
            stored?.Model ?? environment.Model,
            stored?.TimeoutSeconds ?? environment.TimeoutSeconds,
            stored?.MaxOutputTokens ?? environment.MaxOutputTokens,
            stored?.Effort ?? environment.Effort,
            credentialSource != SettingSource.None,
            new EffectiveSources(
                stored is null ? SettingSource.Env : SettingSource.Stored,
                SourceOf(stored?.Model),
                SourceOf(stored?.TimeoutSeconds),
                SourceOf(stored?.MaxOutputTokens),
                SourceOf(stored?.Effort),
                credentialSource));
    }

    private static SettingSource SourceOf(object? storedValue) => storedValue is null ? SettingSource.Env : SettingSource.Stored;

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
