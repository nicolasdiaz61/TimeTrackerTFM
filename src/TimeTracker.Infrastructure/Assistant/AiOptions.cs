namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Proveedores de modelo admitidos por la configuración (<c>Ai:Provider</c>).</summary>
public enum AiProvider
{
    None = 0,
    Anthropic = 1,
    Ollama = 2,
}

/// <summary>Sección <c>Ai</c> de la configuración (variables <c>Ai__*</c>). Sin proveedor el asistente queda deshabilitado.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string? Provider { get; set; }

    public string? Model { get; set; }

    public string? ApiKey { get; set; }

    public string? Endpoint { get; set; }

    public int MaxOutputTokens { get; set; } = 800;

    public int MaxToolIterations { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 25;

    public int RequestsPerMinute { get; set; } = 5;

    public int DailyRequestsPerUser { get; set; } = 50;

    /// <summary>Proveedor configurado, o <c>null</c> si el valor no se reconoce (vacío y <c>None</c> significan deshabilitado).</summary>
    public AiProvider? ResolvedProvider
        => string.IsNullOrWhiteSpace(Provider) ? AiProvider.None
        : Enum.GetValues<AiProvider>().Cast<AiProvider?>().FirstOrDefault(
            candidate => string.Equals(candidate.ToString(), Provider.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool IsEnabled => ResolvedProvider is AiProvider.Anthropic or AiProvider.Ollama;
}
