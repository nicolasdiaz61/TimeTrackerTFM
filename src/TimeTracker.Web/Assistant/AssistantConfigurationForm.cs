using System.Globalization;
using System.Net;

using TimeTracker.Web.Http;
using TimeTracker.Web.Time;

namespace TimeTracker.Web.Assistant;

public enum ConfigurationOperation
{
    Load,
    Save,
    Reset,
    Test,
}

public sealed record ProviderOption(string Value, string Label, bool SupportsEffort);

/// <summary>
/// Estado del formulario de /asistente/configuracion, sin dependencia del navegador: valores editables, mapeo a la
/// petición, limpieza de la clave y traducción de los resultados de la API (DD-17). No contiene reglas de negocio:
/// los límites y la validación los aplica el servidor.
/// </summary>
public sealed class AssistantConfigurationForm
{
    public const string NoneProvider = "None";
    public const string SavedMessage = "Configuración guardada.";
    public const string ResetMessage = "Configuración restablecida al entorno.";
    public const string ReviewFieldsMessage = "Revise los campos indicados.";
    public const string NothingStoredMessage = "No hay configuración guardada: se usan los valores del entorno.";

    private const string EnvironmentSource = "env";

    private static readonly string[] FieldNames = ["provider", "model", "timeoutSeconds", "maxOutputTokens", "effort", "credential"];

    private IReadOnlyDictionary<string, string[]> fieldErrors = new Dictionary<string, string[]>();

    public AssistantConfigurationDto? Loaded { get; private set; }

    public bool IsLoaded => Loaded is not null;

    public bool IsBusy { get; private set; }

    public string Provider { get; set; } = NoneProvider;

    public string? Model { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? MaxOutputTokens { get; set; }

    public string? Effort { get; set; }

    /// <summary>Solo vive aquí, en el campo de contraseña: se vacía tras guardar y nunca se rellena desde la API.</summary>
    public string? Credential { get; set; }

    public IReadOnlyList<ProviderOption> ProviderOptions { get; private set; } = [];

    public bool EffortVisible => ProviderOptions.FirstOrDefault(option => option.Value == Provider)?.SupportsEffort ?? false;

    public bool CredentialEnabled => Loaded?.CredentialStorageAvailable ?? false;

    public bool CredentialConfigured => Loaded?.CredentialConfigured ?? false;

    public string? ErrorMessage { get; private set; }

    public string? SuccessMessage { get; private set; }

    public bool ReloadOffered { get; private set; }

    public bool ResetConfirmationPending { get; private set; }

    public string? TestMessage { get; private set; }

    public bool TestSucceeded { get; private set; }

    /// <summary>Mensajes del 400 de campos que la pantalla no muestra (p. ej. el token de concurrencia).</summary>
    public IReadOnlyList<string> OtherErrors => [.. fieldErrors.Where(pair => !FieldNames.Contains(pair.Key)).SelectMany(pair => pair.Value)];

    public IReadOnlyList<string> ErrorsFor(string field) => fieldErrors.TryGetValue(field, out var messages) ? messages : [];

    public static string SourceLabel(string source) => source switch
    {
        EnvironmentSource => "Entorno",
        "stored" => "Guardado por un responsable",
        "none" => "Sin clave",
        _ => source,
    };

    /// <summary>Cuándo y quién guardó la configuración vigente, con la hora del usuario; el nombre se muestra como texto.</summary>
    public string UpdatedText(ILocalTimeZone zone)
    {
        if (Loaded?.UpdatedAt is not { } updatedAt)
        {
            return NothingStoredMessage;
        }

        var local = updatedAt.ToOffset(zone.OffsetAt(updatedAt));
        var text = string.Create(CultureInfo.InvariantCulture, $"Última modificación: {local:dd/MM/yyyy HH:mm}");
        return Loaded.UpdatedBy is { Length: > 0 } author ? $"{text} por {author}." : text + ".";
    }

    /// <summary>Inicia una operación y limpia los mensajes anteriores; <see langword="false"/> si ya hay una en curso.</summary>
    public bool TryBegin()
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;
        TestMessage = null;
        ReloadOffered = false;
        fieldErrors = new Dictionary<string, string[]>();
        return true;
    }

    public void RequestReset()
    {
        ResetConfirmationPending = !IsBusy;
    }

    public void CancelReset()
    {
        ResetConfirmationPending = false;
    }

    /// <summary>Consume la confirmación pendiente e inicia el restablecimiento; sin confirmación previa no hace nada.</summary>
    public bool TryConfirmReset()
    {
        if (!ResetConfirmationPending)
        {
            return false;
        }

        ResetConfirmationPending = false;
        return TryBegin();
    }

    public void CompleteLoad(AssistantConfigurationDto configuration) => Apply(configuration, null);

    public void CompleteSave(AssistantConfigurationDto configuration) => Apply(configuration, SavedMessage);

    public void CompleteReset(AssistantConfigurationDto configuration) => Apply(configuration, ResetMessage);

    /// <summary>La clave tecleada se conserva: la prueba no la guarda y el Manager puede guardar después.</summary>
    public void CompleteTest(AssistantConnectionTestResponseDto outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        TestMessage = outcome.Message;
        TestSucceeded = string.Equals(outcome.Result, "ok", StringComparison.OrdinalIgnoreCase);
        IsBusy = false;
    }

    public void Fail(ConfigurationOperation operation, AssistantConfigurationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        IsBusy = false;
        ResetConfirmationPending = false;
        if (operation is ConfigurationOperation.Save or ConfigurationOperation.Reset)
        {
            Credential = null;
        }

        var message = error.Exception.Message;
        switch (error.Exception.StatusCode)
        {
            case HttpStatusCode.Conflict:
                ErrorMessage = message;
                ReloadOffered = true;
                break;
            case HttpStatusCode.BadRequest:
                fieldErrors = error.FieldErrors;
                ErrorMessage = error.FieldErrors.Count > 0 ? ReviewFieldsMessage : message;
                break;
            case HttpStatusCode.TooManyRequests:
                ErrorMessage = message + WaitHint.Describe(error.RetryAfterSeconds);
                break;
            default:
                ErrorMessage = message;
                break;
        }
    }

    public AssistantConfigurationRequestDto BuildSaveRequest()
    {
        var loaded = RequireLoaded();
        return new AssistantConfigurationRequestDto(
            Provider,
            ModelToSend(loaded),
            TimeoutToSend(loaded),
            TokensToSend(loaded),
            EffortToSend(loaded),
            CredentialToSend(),
            loaded.ConcurrencyToken);
    }

    public AssistantConnectionTestRequestDto BuildTestRequest()
    {
        var loaded = RequireLoaded();
        return new AssistantConnectionTestRequestDto(Provider, ModelToSend(loaded), TimeoutToSend(loaded), TokensToSend(loaded), EffortToSend(loaded), CredentialToSend());
    }

    private void Apply(AssistantConfigurationDto configuration, string? success)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Loaded = configuration;
        Provider = configuration.Provider;
        Model = configuration.Model;
        TimeoutSeconds = configuration.TimeoutSeconds;
        MaxOutputTokens = configuration.MaxOutputTokens;
        Effort = configuration.Effort;
        Credential = null;
        ProviderOptions = BuildOptions(configuration);
        SuccessMessage = success;
        ErrorMessage = null;
        ReloadOffered = false;
        ResetConfirmationPending = false;
        fieldErrors = new Dictionary<string, string[]>();
        IsBusy = false;
    }

    private static List<ProviderOption> BuildOptions(AssistantConfigurationDto configuration)
    {
        var options = configuration.AllowedProviders
            .Where(allowed => allowed.Provider != NoneProvider)
            .Select(allowed => new ProviderOption(allowed.Provider, allowed.Provider, allowed.SupportsEffort))
            .ToList();

        // El proveedor vigente sigue seleccionable aunque la lista permitida ya no lo incluya.
        if (configuration.Provider != NoneProvider && options.All(option => option.Value != configuration.Provider))
        {
            options.Add(new ProviderOption(configuration.Provider, configuration.Provider, false));
        }

        options.Add(new ProviderOption(NoneProvider, "Deshabilitado", false));
        return options;
    }

    private AssistantConfigurationDto RequireLoaded()
        => Loaded ?? throw new InvalidOperationException("La configuración aún no se ha cargado.");

    // Un valor del entorno que no se ha tocado se omite para no congelarlo como guardado. Si cambia el proveedor,
    // el valor del entorno pertenece al proveedor anterior: se envía todo de forma explícita.
    private bool KeepsEnvironment(AssistantConfigurationDto loaded, string source, bool unchanged)
        => unchanged && source == EnvironmentSource && Provider == loaded.Provider;

    private string? ModelToSend(AssistantConfigurationDto loaded)
    {
        var model = string.IsNullOrWhiteSpace(Model) ? null : Model.Trim();
        return KeepsEnvironment(loaded, loaded.Sources.Model, model == loaded.Model) ? null : model;
    }

    private int? TimeoutToSend(AssistantConfigurationDto loaded)
        => KeepsEnvironment(loaded, loaded.Sources.TimeoutSeconds, TimeoutSeconds == loaded.TimeoutSeconds) ? null : TimeoutSeconds;

    private int? TokensToSend(AssistantConfigurationDto loaded)
        => KeepsEnvironment(loaded, loaded.Sources.MaxOutputTokens, MaxOutputTokens == loaded.MaxOutputTokens) ? null : MaxOutputTokens;

    private string? EffortToSend(AssistantConfigurationDto loaded)
    {
        var effort = EffortVisible && !string.IsNullOrWhiteSpace(Effort) ? Effort : null;
        return KeepsEnvironment(loaded, loaded.Sources.Effort, effort == loaded.Effort) ? null : effort;
    }

    private string? CredentialToSend()
        => CredentialEnabled && !string.IsNullOrWhiteSpace(Credential) ? Credential : null;
}
