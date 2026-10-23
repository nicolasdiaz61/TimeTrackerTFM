using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Valida <c>Ai:*</c> al arrancar. Sin proveedor solo se comprueba <c>Ai:AllowedProviders</c>; los mensajes nombran la
/// clave y nunca repiten el valor configurado (puede ser una clave de API). Las claves maestras no se validan.
/// </summary>
public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public const int MinToolIterations = 1;
    public const int MaxToolIterations = 10;
    public const int MinRequestsPerMinute = 1;
    public const int MaxRequestsPerMinute = 60;
    public const int MinDailyRequestsPerUser = 1;
    public const int MaxDailyRequestsPerUser = 1000;
    public const int MinRequestsPerMinutePerCompany = 1;
    public const int MaxRequestsPerMinutePerCompany = 600;
    public const int MinDailyRequestsPerCompany = 1;
    public const int MaxDailyRequestsPerCompany = 100000;
    public const int MinLoadedCompanies = 1;
    public const int MaxLoadedCompaniesLimit = 1000;
    public const int MinRuntimeIdleMinutes = 1;
    public const int MaxRuntimeIdleMinutes = 1440;
    private const int MinSettingsPollSeconds = 1;
    private const int MaxSettingsPollSeconds = 300;

    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();
        var provider = options.ResolvedProvider;
        if (provider is null)
        {
            failures.Add("Ai:Provider: el valor no es válido. Use Anthropic, OpenAI, Gemini, Groq, Ollama o déjelo vacío para deshabilitar el asistente.");
        }

        // Se validan siempre, aunque el entorno no tenga proveedor: limitan lo que cualquier compañía puede hacer.
        CheckAllowedProviders(failures, options);
        CheckCompanyLimits(failures, options);
        if (provider is null)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        if (provider == AiProvider.None)
        {
            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        var traits = AssistantProviderTraits.For(provider.Value.ToAssistantProvider());
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("Ai:Model: debe definirse con el nombre del modelo.");
        }
        else if (options.Model.Length > AssistantSettingsLimits.MaxModelLength)
        {
            failures.Add($"Ai:Model: debe tener como máximo {AssistantSettingsLimits.MaxModelLength} caracteres.");
        }

        if (traits.RequiresCredential && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("Ai:ApiKey: debe definirse con la clave de API del proveedor.");
        }

        if (traits.RequiresEnvironmentEndpoint && !IsHttpUri(options.Endpoint))
        {
            failures.Add("Ai:Endpoint: debe ser una dirección http o https absoluta.");
        }

        if (!string.IsNullOrWhiteSpace(options.Effort) && options.ResolvedEffort is null)
        {
            failures.Add("Ai:Effort: debe ser low, medium o high.");
        }

        CheckRange(failures, nameof(AiOptions.MaxOutputTokens), options.MaxOutputTokens, AssistantSettingsLimits.MinOutputTokens, AssistantSettingsLimits.MaxOutputTokens);
        CheckRange(failures, nameof(AiOptions.MaxToolIterations), options.MaxToolIterations, MinToolIterations, MaxToolIterations);
        CheckRange(failures, nameof(AiOptions.TimeoutSeconds), options.TimeoutSeconds, AssistantSettingsLimits.MinTimeoutSeconds, AssistantSettingsLimits.MaxTimeoutSeconds);
        CheckRange(failures, nameof(AiOptions.RequestsPerMinute), options.RequestsPerMinute, MinRequestsPerMinute, MaxRequestsPerMinute);
        CheckRange(failures, nameof(AiOptions.DailyRequestsPerUser), options.DailyRequestsPerUser, MinDailyRequestsPerUser, MaxDailyRequestsPerUser);
        CheckRange(failures, nameof(AiOptions.SettingsPollSeconds), options.SettingsPollSeconds, MinSettingsPollSeconds, MaxSettingsPollSeconds);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void CheckCompanyLimits(List<string> failures, AiOptions options)
    {
        CheckRange(failures, nameof(AiOptions.RequestsPerMinutePerCompany), options.RequestsPerMinutePerCompany, MinRequestsPerMinutePerCompany, MaxRequestsPerMinutePerCompany);
        CheckRange(failures, nameof(AiOptions.DailyRequestsPerCompany), options.DailyRequestsPerCompany, MinDailyRequestsPerCompany, MaxDailyRequestsPerCompany);
        CheckRange(failures, nameof(AiOptions.MaxLoadedCompanies), options.MaxLoadedCompanies, MinLoadedCompanies, MaxLoadedCompaniesLimit);
        CheckRange(failures, nameof(AiOptions.RuntimeIdleMinutes), options.RuntimeIdleMinutes, MinRuntimeIdleMinutes, MaxRuntimeIdleMinutes);
    }

    // La lista limita lo que un Manager puede elegir aunque el entorno no tenga proveedor, así que se valida siempre.
    private static void CheckAllowedProviders(List<string> failures, AiOptions options)
    {
        if (AllowedProviders.Validate(options.AllowedProviders) is { } error)
        {
            failures.Add(error);
        }
    }

    private static bool IsHttpUri(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static void CheckRange(List<string> failures, string key, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            failures.Add($"Ai:{key}: debe estar entre {min} y {max}.");
        }
    }
}
