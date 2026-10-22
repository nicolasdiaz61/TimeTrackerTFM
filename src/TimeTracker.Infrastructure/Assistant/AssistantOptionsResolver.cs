using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Convierte la fila guardada de una compañía en las opciones con las que se crea su cliente (DD-5, DD-13). Revalida antes de
/// crear nada: una fila inválida, no descifrable o con un proveedor ya no permitido no produce opciones, y quien llama decide qué hacer.
/// </summary>
internal sealed class AssistantOptionsResolver(IOptions<AiOptions> options, IAssistantEnvironment environment, IAssistantCredentialProtector protector)
{
    public const string InvalidCategory = "invalid";
    public const string UndecryptableCategory = "undecryptable";
    public const string NotAllowedCategory = "not-allowed";

    /// <summary>Valores de plataforma del entorno, incluida la clave: solo para la compañía con <c>uses_platform_assistant</c>.</summary>
    public AiOptions Platform() => options.Value.Copy();

    /// <summary>Opciones sin proveedor, modelo ni clave: el asistente deshabilitado de una compañía.</summary>
    public AiOptions Disabled()
    {
        var disabled = options.Value.Copy();
        disabled.Provider = AssistantProvider.None.ToString();
        disabled.Model = null;
        disabled.ApiKey = null;
        return disabled;
    }

    /// <summary>Las mismas reglas que el guardado más las del entorno, antes de descifrar con efecto ni crear nada.</summary>
    public (AiOptions? Options, string? Category) Resolve(AssistantSettings stored, bool usesPlatform, Guid companyId)
    {
        var env = environment.Current.ForCompany(usesPlatform);
        if (stored.Provider != AssistantProvider.None && !AllowedProviders.Resolve(env).Providers.Contains(stored.Provider))
        {
            return (null, NotAllowedCategory);
        }

        AssistantCredential? plain = null;
        if (stored.Provider != AssistantProvider.None && stored.Credential is { } encrypted && !protector.TryUnprotect(encrypted, stored.Provider, companyId, out plain))
        {
            return (null, UndecryptableCategory);
        }

        if (!AssistantSettingsRules.Validate(ToInput(stored), env, stored, protector, companyId).IsValid)
        {
            return (null, InvalidCategory);
        }

        var effective = AssistantSettingsResolver.Resolve(env, stored, storedCredentialUsable: plain is not null, usesPlatform);
        var resolved = options.Value.Copy();
        resolved.Provider = effective.Provider.ToString();
        resolved.Model = effective.Model;
        resolved.TimeoutSeconds = effective.TimeoutSeconds;
        resolved.MaxOutputTokens = effective.MaxOutputTokens;
        resolved.Effort = effective.Effort?.ToString();
        resolved.ApiKey = effective.Sources.Credential switch
        {
            SettingSource.Stored => plain?.Value,
            SettingSource.Env => env.Credential?.Value,
            _ => null,
        };
        return (resolved, null);
    }

    private static AssistantSettingsInput ToInput(AssistantSettings stored)
        => new(stored.Provider.ToString(), stored.Model, stored.TimeoutSeconds, stored.MaxOutputTokens, stored.Effort?.ToString(), Credential: null);
}
