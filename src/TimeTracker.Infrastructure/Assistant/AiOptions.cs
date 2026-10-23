using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Proveedores admitidos por la configuración (<c>Ai:Provider</c>). Los nombres coinciden con <c>AssistantProvider</c>
/// de Application (una prueba lo fija); los valores existentes no se renumeran.
/// </summary>
public enum AiProvider
{
    None = 0,
    Anthropic = 1,
    Ollama = 2,
    OpenAI = 3,
    Gemini = 4,
    Groq = 5,
}

/// <summary>Sección <c>Ai</c> de la configuración (variables <c>Ai__*</c>). Sin proveedor el asistente queda deshabilitado.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string? Provider { get; set; }

    public string? Model { get; set; }

    public string? ApiKey { get; set; }

    public string? Endpoint { get; set; }

    public string? Effort { get; set; }

    /// <summary>Lista separada por comas de proveedores seleccionables por un Manager (DD-4).</summary>
    public string? AllowedProviders { get; set; }

    /// <summary>Claves maestras en Base64 por versión (<c>Ai__MasterKeys__1</c>). No se validan al arrancar: sin ellas el cifrado no está disponible.</summary>
    public Dictionary<string, string?> MasterKeys { get; set; } = [];

    // Texto y no entero: un valor no numérico dejaría el cifrado no disponible en vez de impedir el arranque al enlazar.
    public string? ActiveMasterKeyVersion { get; set; }

    public int SettingsPollSeconds { get; set; } = 30;

    public int MaxOutputTokens { get; set; } = 800;

    public int MaxToolIterations { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 25;

    public int RequestsPerMinute { get; set; } = 5;

    public int DailyRequestsPerUser { get; set; } = 50;

    /// <summary>Peticiones por minuto de toda una compañía (ventana fija, DD-16). Del entorno: el Manager no la edita.</summary>
    public int RequestsPerMinutePerCompany { get; set; } = 30;

    /// <summary>Peticiones por día natural de Europe/Madrid de toda una compañía (DD-16). Del entorno: el Manager no la edita.</summary>
    public int DailyRequestsPerCompany { get; set; } = 500;

    /// <summary>Número máximo de compañías con proveedor cargado a la vez en el registro de instantáneas (DD-14).</summary>
    public int MaxLoadedCompanies { get; set; } = 100;

    /// <summary>Minutos sin preguntas tras los que se descarga la instantánea de una compañía (DD-14).</summary>
    public int RuntimeIdleMinutes { get; set; } = 60;

    /// <summary>Proveedor configurado, o <c>null</c> si el valor no se reconoce (vacío y <c>None</c> significan deshabilitado).</summary>
    public AiProvider? ResolvedProvider
        => string.IsNullOrWhiteSpace(Provider) ? AiProvider.None
        : Enum.GetValues<AiProvider>().Cast<AiProvider?>().FirstOrDefault(
            candidate => string.Equals(candidate.ToString(), Provider.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool IsEnabled => ResolvedProvider is not (null or AiProvider.None);

    /// <summary>Copia independiente: modificar la original o la copia no afecta a la otra (el diccionario también se duplica).</summary>
    internal AiOptions Copy()
    {
        var copy = (AiOptions)MemberwiseClone();
        copy.MasterKeys = new Dictionary<string, string?>(MasterKeys);
        return copy;
    }

    /// <summary>Esfuerzo configurado por nombre, o <c>null</c> si falta o no es <c>low</c>, <c>medium</c> o <c>high</c> (nunca por número).</summary>
    public AssistantEffort? ResolvedEffort
        => Enum.GetValues<AssistantEffort>().Cast<AssistantEffort?>().FirstOrDefault(
            candidate => string.Equals(candidate.ToString(), Effort?.Trim(), StringComparison.OrdinalIgnoreCase));
}
