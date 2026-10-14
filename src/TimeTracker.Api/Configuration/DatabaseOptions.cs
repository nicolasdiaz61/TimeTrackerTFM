namespace TimeTracker.Api.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Aplica las migraciones pendientes al arrancar. Desactivado por defecto: el despliegue decide.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}
