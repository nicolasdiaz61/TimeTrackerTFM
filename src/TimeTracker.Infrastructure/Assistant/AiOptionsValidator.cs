using Microsoft.Extensions.Options;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Valida <c>Ai:*</c> al arrancar. Sin proveedor no se comprueba nada más; los mensajes nombran la clave y nunca
/// repiten el valor configurado (puede ser una clave de API).
/// </summary>
public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    private const int MaxModelLength = 100;

    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var provider = options.ResolvedProvider;
        if (provider is null)
        {
            return ValidateOptionsResult.Fail("Ai:Provider: el valor no es válido. Use Anthropic, Ollama o déjelo vacío para deshabilitar el asistente.");
        }

        if (provider == AiProvider.None)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("Ai:Model: debe definirse con el nombre del modelo.");
        }
        else if (options.Model.Length > MaxModelLength)
        {
            failures.Add($"Ai:Model: debe tener como máximo {MaxModelLength} caracteres.");
        }

        if (provider == AiProvider.Anthropic && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("Ai:ApiKey: debe definirse con la clave de API del proveedor.");
        }

        if (provider == AiProvider.Ollama && !IsHttpUri(options.Endpoint))
        {
            failures.Add("Ai:Endpoint: debe ser una dirección http o https absoluta.");
        }

        CheckRange(failures, nameof(AiOptions.MaxOutputTokens), options.MaxOutputTokens, 64, 4096);
        CheckRange(failures, nameof(AiOptions.MaxToolIterations), options.MaxToolIterations, 1, 10);
        CheckRange(failures, nameof(AiOptions.TimeoutSeconds), options.TimeoutSeconds, 5, 55);
        CheckRange(failures, nameof(AiOptions.RequestsPerMinute), options.RequestsPerMinute, 1, 60);
        CheckRange(failures, nameof(AiOptions.DailyRequestsPerUser), options.DailyRequestsPerUser, 1, 1000);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
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
