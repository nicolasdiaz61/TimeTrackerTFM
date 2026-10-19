namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Reglas puras de validación; los mensajes nombran el campo y nunca incluyen valores recibidos.</summary>
public static class AssistantSettingsRules
{
    public static SettingsValidation Validate(AssistantSettingsInput input, AssistantEnvironmentDefaults environment, AssistantSettings? stored)
    {
        var errors = new List<FieldError>();
        var provider = ParseProvider(input.Provider, environment, errors);
        var model = ValidateModel(input.Model, errors);
        var timeout = ValidateRange("timeoutSeconds", input.TimeoutSeconds, AssistantSettingsLimits.MinTimeoutSeconds, AssistantSettingsLimits.MaxTimeoutSeconds, errors);
        var tokens = ValidateRange("maxOutputTokens", input.MaxOutputTokens, AssistantSettingsLimits.MinOutputTokens, AssistantSettingsLimits.MaxOutputTokens, errors);
        var effort = ParseEffort(input.Effort, provider, errors);
        var credential = string.IsNullOrWhiteSpace(input.Credential) ? null : new AssistantCredential(input.Credential);

        if (provider is { } selected && selected != AssistantProvider.None)
        {
            ValidateProviderRequirements(selected, input.Model is null ? environment.Model : model, credential, environment, stored, errors);
        }

        return errors.Count > 0
            ? new SettingsValidation(errors, null)
            : new SettingsValidation([], new ValidatedSettingsInput(provider!.Value, model, timeout, tokens, effort, credential));
    }

    /// <summary>Devuelve el error del token de concurrencia, o <c>null</c> si es válido («0» = sin fila).</summary>
    public static FieldError? ValidateToken(string? token)
        => token is { Length: > 0 } && token.All(char.IsAsciiDigit)
            ? null
            : new FieldError("concurrencyToken", "El campo concurrencyToken es obligatorio y debe ser numérico.");

    private static AssistantProvider? ParseProvider(string? value, AssistantEnvironmentDefaults environment, List<FieldError> errors)
    {
        var provider = Enum.GetValues<AssistantProvider>()
            .Cast<AssistantProvider?>()
            .FirstOrDefault(p => string.Equals(p.ToString(), value?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            errors.Add(new FieldError("provider", "El campo provider debe ser un proveedor del catálogo."));
        }
        else if (provider != AssistantProvider.None && !AllowedProviders.Resolve(environment).Contains(provider.Value))
        {
            errors.Add(new FieldError("provider", "El proveedor indicado en provider no está permitido en esta instalación."));
        }

        return provider;
    }

    private static string? ValidateModel(string? model, List<FieldError> errors)
    {
        if (model is null)
        {
            return null;
        }

        if (model.Length is 0 or > AssistantSettingsLimits.MaxModelLength || !model.All(IsModelCharacter))
        {
            errors.Add(new FieldError("model", $"El campo model debe tener entre 1 y {AssistantSettingsLimits.MaxModelLength} caracteres: letras ASCII, dígitos y . _ - : / @."));
            return null;
        }

        return model;
    }

    private static bool IsModelCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':' or '/' or '@';

    private static int? ValidateRange(string field, int? value, int min, int max, List<FieldError> errors)
    {
        if (value is { } number && (number < min || number > max))
        {
            errors.Add(new FieldError(field, $"El campo {field} debe estar entre {min} y {max}."));
            return null;
        }

        return value;
    }

    private static AssistantEffort? ParseEffort(string? value, AssistantProvider? provider, List<FieldError> errors)
    {
        if (value is null)
        {
            return null;
        }

        var effort = Enum.GetValues<AssistantEffort>()
            .Cast<AssistantEffort?>()
            .FirstOrDefault(e => string.Equals(e.ToString(), value, StringComparison.OrdinalIgnoreCase));
        if (effort is null)
        {
            errors.Add(new FieldError("effort", "El campo effort debe ser low, medium o high."));
        }
        else if (provider is { } selected && !AssistantProviderTraits.For(selected).SupportsEffort)
        {
            errors.Add(new FieldError("effort", "El proveedor seleccionado no admite el campo effort."));
        }

        return effort;
    }

    private static void ValidateProviderRequirements(
        AssistantProvider provider,
        string? effectiveModel,
        AssistantCredential? newCredential,
        AssistantEnvironmentDefaults environment,
        AssistantSettings? stored,
        List<FieldError> errors)
    {
        var traits = AssistantProviderTraits.For(provider);
        if (string.IsNullOrEmpty(effectiveModel) && !errors.Any(e => e.Field == "model"))
        {
            errors.Add(new FieldError("model", "El campo model es obligatorio: no hay modelo en el entorno."));
        }

        if (traits.RequiresEnvironmentEndpoint && !environment.HasEndpoint)
        {
            errors.Add(new FieldError("provider", "El proveedor indicado en provider necesita un endpoint definido en el entorno."));
        }

        var hasCredential = newCredential is not null
            || (stored is { Credential: not null } && stored.Provider == provider)
            || (environment.Credential is not null && environment.Provider == provider);
        if (traits.RequiresCredential && !hasCredential)
        {
            errors.Add(new FieldError("credential", "El proveedor seleccionado necesita una clave en el campo credential."));
        }
    }
}
