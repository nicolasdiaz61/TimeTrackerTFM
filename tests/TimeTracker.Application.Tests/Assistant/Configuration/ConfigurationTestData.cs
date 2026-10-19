using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

internal static class ConfigurationTestData
{
    public const string SecretKey = "clave-prueba-123456";

    public static AssistantEnvironmentDefaults Env(
        AssistantProvider provider = AssistantProvider.Anthropic,
        string? model = "env-m",
        int timeoutSeconds = 30,
        int maxOutputTokens = 1024,
        AssistantEffort? effort = null,
        string? apiKey = SecretKey,
        bool hasEndpoint = false,
        string? allowedProviders = null)
        => new(
            provider,
            model,
            timeoutSeconds,
            maxOutputTokens,
            effort,
            apiKey is null ? null : new AssistantCredential(apiKey),
            hasEndpoint,
            allowedProviders);

    public static AssistantSettingsInput Input(
        string? provider = "Anthropic",
        string? model = "m-1",
        int? timeoutSeconds = 25,
        int? maxOutputTokens = 800,
        string? effort = null,
        string? credential = null)
        => new(provider, model, timeoutSeconds, maxOutputTokens, effort, credential);

    public static AssistantSettings Stored(
        AssistantProvider provider = AssistantProvider.Anthropic,
        string? model = "db-m",
        int? timeoutSeconds = null,
        int? maxOutputTokens = null,
        AssistantEffort? effort = null,
        EncryptedCredential? credential = null,
        string version = "10")
        => new(provider, model, timeoutSeconds, maxOutputTokens, effort, credential, Guid.NewGuid(), DateTimeOffset.UnixEpoch, version);
}
