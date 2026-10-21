namespace TimeTracker.Application.Assistant.Configuration;

public sealed record AllowedProviderInfo(AssistantProvider Provider, bool RequiresCredential, bool SupportsEffort);

/// <summary>Lo que devuelven la consulta y el guardado. Nunca contiene la clave, ni en claro ni cifrada.</summary>
public sealed record AssistantSettingsView(
    EffectiveAssistantSettings Effective,
    bool CredentialStorageAvailable,
    IReadOnlyList<AllowedProviderInfo> AllowedProviders,
    string ConcurrencyToken,
    DateTimeOffset? UpdatedAt,
    Guid? UpdatedBy,
    bool Applied)
{
    /// <summary>
    /// Construye la vista sobre la fila guardada. Una fila cuyo proveedor ya no está permitido no se aplica:
    /// los valores efectivos son entonces los del entorno, pero el token sigue siendo el de la fila.
    /// </summary>
    internal static AssistantSettingsView Build(AssistantEnvironmentDefaults environment, IAssistantCredentialProtector protector, AssistantSettings? stored, Guid companyId)
    {
        var allowed = Configuration.AllowedProviders.Resolve(environment).Providers;
        var applied = stored is not null && (stored.Provider == AssistantProvider.None || allowed.Contains(stored.Provider));
        var inUse = applied ? stored : null;
        var credentialUsable = inUse is not null && UsableCredential.FromStored(inUse, inUse.Provider, protector, companyId) is not null;

        return new AssistantSettingsView(
            AssistantSettingsResolver.Resolve(environment, inUse, credentialUsable),
            protector.IsAvailable,
            [.. allowed.Select(p => new AllowedProviderInfo(p, AssistantProviderTraits.For(p).RequiresCredential, AssistantProviderTraits.For(p).SupportsEffort))],
            stored?.Version ?? "0",
            stored?.UpdatedAt,
            stored?.UpdatedBy,
            applied);
    }
}
