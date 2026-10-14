namespace TimeTracker.Infrastructure.Persistence;

/// <summary>Sección <c>ConnectionStrings</c> de la configuración; solo se usa <c>Default</c>.</summary>
public sealed class ConnectionStringOptions
{
    public const string SectionName = "ConnectionStrings";

    public string? Default { get; set; }
}
