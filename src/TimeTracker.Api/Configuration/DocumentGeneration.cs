using System.Reflection;

namespace TimeTracker.Api.Configuration;

/// <summary>
/// Al compilar, <c>dotnet-getdocument</c> arranca la aplicación para extraer el contrato OpenAPI y ahí no existe
/// la configuración real de despliegue ni una base de datos. Solo en ese proceso se fuerza el entorno
/// <c>Development</c> y se aportan valores ficticios, sin relajar ninguna validación (DD-16, ADR 0009).
/// La ejecución normal no se ve afectada: sigue exigiendo la configuración real.
/// </summary>
internal static class DocumentGeneration
{
    private const string ToolAssemblyName = "GetDocument.Insider";

    private static readonly Dictionary<string, string?> PlaceholderSettings = new()
    {
        ["Cors:AllowedOrigins:0"] = "https://openapi.example.test",
        // La conexión es perezosa: el servidor inexistente nunca se contacta y no hay contraseña.
        ["ConnectionStrings:Default"] = "Host=db.invalid;Database=openapi;Username=openapi",
        // Aunque el entorno del desarrollador la active: la exportación del contrato nunca debe tocar la base de datos.
        ["Database:ApplyMigrationsOnStartup"] = "false",
        // Clave inventada de más de 32 bytes: solo existe en el proceso de exportación del contrato (V21).
        ["Jwt:SigningKey"] = "clave-ficticia-solo-para-exportar-openapi-0123456789",
        // La exportación del contrato nunca siembra usuarios (V21).
        ["Seed:DemoUsers"] = "false",
    };

    public static bool IsRunningUnderTool { get; } = Assembly.GetEntryAssembly()?.GetName().Name == ToolAssemblyName;

    public static WebApplicationOptions CreateOptions(string[] args) => new()
    {
        Args = args,
        EnvironmentName = IsRunningUnderTool ? Environments.Development : null,
    };

    public static void ApplyPlaceholderSettings(ConfigurationManager configuration)
    {
        if (IsRunningUnderTool)
        {
            configuration.AddInMemoryCollection(PlaceholderSettings);
        }
    }
}
