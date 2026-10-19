namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Proveedores seleccionables (sin <c>None</c>, que siempre lo es). <paramref name="Error"/> nombra la clave, nunca el valor.</summary>
public sealed record AllowedProvidersResult(IReadOnlyList<AssistantProvider> Providers, string? Error);

public static class AllowedProviders
{
    public const string ConfigurationKey = "Ai:AllowedProviders";

    private const string InvalidListError = ConfigurationKey + " contiene un nombre que no es un proveedor del catálogo.";

    /// <summary>Solo valida la lista (vacía es válida): devuelve el error, que nombra la clave y nunca el valor, o <c>null</c>.</summary>
    public static string? Validate(string? raw)
        => string.IsNullOrWhiteSpace(raw) || TryParseList(raw, out _) ? null : InvalidListError;

    public static AllowedProvidersResult Resolve(AssistantEnvironmentDefaults environment)
    {
        IEnumerable<AssistantProvider> providers;
        if (string.IsNullOrWhiteSpace(environment.AllowedProviders))
        {
            providers = environment.Provider == AssistantProvider.None ? [] : [environment.Provider];
        }
        else if (!TryParseList(environment.AllowedProviders, out var listed))
        {
            return new AllowedProvidersResult([], InvalidListError);
        }
        else
        {
            providers = listed;
        }

        return new AllowedProvidersResult([.. providers.Where(p => environment.HasEndpoint || !AssistantProviderTraits.For(p).RequiresEnvironmentEndpoint).Distinct()], null);
    }

    private static bool TryParseList(string list, out List<AssistantProvider> providers)
    {
        providers = [];
        foreach (var name in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var provider = Enum.GetValues<AssistantProvider>()
                .Cast<AssistantProvider?>()
                .FirstOrDefault(p => string.Equals(p.ToString(), name, StringComparison.OrdinalIgnoreCase));
            if (provider is null or AssistantProvider.None)
            {
                return false;
            }

            providers.Add(provider.Value);
        }

        return true;
    }
}
