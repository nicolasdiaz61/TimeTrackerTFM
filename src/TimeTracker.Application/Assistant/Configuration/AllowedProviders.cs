namespace TimeTracker.Application.Assistant.Configuration;

public static class AllowedProviders
{
    public static IReadOnlyList<AssistantProvider> Resolve(AssistantEnvironmentDefaults environment)
    {
        if (string.IsNullOrWhiteSpace(environment.AllowedProviders))
        {
            return [environment.Provider];
        }

        return [.. environment.AllowedProviders
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.Parse<AssistantProvider>(name, ignoreCase: true))
            .Distinct()];
    }
}
