namespace TimeTracker.Api.IntegrationTests;

/// <summary>
/// R7 y V19: las variables de entorno de la máquina tienen prioridad sobre <c>UseSetting</c> y falsearían las
/// pruebas de arranque. Se borran una vez, antes de crear ninguna fábrica, del proceso de pruebas.
/// </summary>
internal static class EnvironmentIsolation
{
    private static readonly string[] Prefixes = ["Cors__", "ConnectionStrings__", "Database__", "Jwt__", "Seed__"];

    private const string EnvironmentName = "ASPNETCORE_ENVIRONMENT";

    public static void Apply()
    {
        var names = Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .Where(name => name.Equals(EnvironmentName, StringComparison.OrdinalIgnoreCase)
                || Prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var name in names)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }
}
