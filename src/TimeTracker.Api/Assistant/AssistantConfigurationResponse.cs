using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Api.Assistant;

/// <summary>Origen de cada valor efectivo: <c>env</c> o <c>stored</c> (la clave también puede ser <c>none</c>).</summary>
public sealed record AssistantConfigurationSources(string Provider, string Model, string TimeoutSeconds, string MaxOutputTokens, string Effort, string Credential);

public sealed record AllowedProviderResponse(string Provider, bool RequiresCredential, bool SupportsEffort);

/// <summary>
/// Configuración efectiva. No tiene ningún campo que pueda contener la clave (ni en claro ni cifrada): solo el indicador
/// <c>credentialConfigured</c>.
/// </summary>
public sealed record AssistantConfigurationResponse(
    string Provider,
    string? Model,
    int TimeoutSeconds,
    int MaxOutputTokens,
    string? Effort,
    AssistantConfigurationSources Sources,
    bool CredentialConfigured,
    bool CredentialStorageAvailable,
    IReadOnlyList<AllowedProviderResponse> AllowedProviders,
    string ConcurrencyToken,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    bool Applied)
{
    public static AssistantConfigurationResponse From(AssistantSettingsView view, string? updatedByName)
    {
        var effective = view.Effective;
        return new AssistantConfigurationResponse(
            effective.Provider.ToString(),
            effective.Model,
            effective.TimeoutSeconds,
            effective.MaxOutputTokens,
            effective.Effort?.ToString().ToLowerInvariant(),
            new AssistantConfigurationSources(
                Source(effective.Sources.Provider),
                Source(effective.Sources.Model),
                Source(effective.Sources.TimeoutSeconds),
                Source(effective.Sources.MaxOutputTokens),
                Source(effective.Sources.Effort),
                Source(effective.Sources.Credential)),
            effective.CredentialConfigured,
            view.CredentialStorageAvailable,
            [.. view.AllowedProviders.Select(allowed => new AllowedProviderResponse(allowed.Provider.ToString(), allowed.RequiresCredential, allowed.SupportsEffort))],
            view.ConcurrencyToken,
            view.UpdatedAt,
            updatedByName,
            view.Applied);
    }

    private static string Source(SettingSource source) => source.ToString().ToLowerInvariant();
}

/// <summary>Resultado de la prueba: una categoría fija y un texto fijo; nunca el mensaje del proveedor.</summary>
public sealed record AssistantConnectionTestResponse(string Result, string Message);
