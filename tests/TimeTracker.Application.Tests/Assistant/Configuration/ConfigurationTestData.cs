using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

internal static class ConfigurationTestData
{
    public const string SecretKey = "clave-prueba-123456";

    /// <summary>Compañías de prueba: la compañía por defecto es la de plataforma (demo); cualquier otra no lo es salvo que se siembre.</summary>
    public static InMemoryCompanyRepository PlatformCompanies(params Guid[] platformCompanies)
    {
        var companies = new InMemoryCompanyRepository();
        companies.Seed(Company.DefaultId, "Demo", usesPlatformAssistant: true);
        foreach (var id in platformCompanies)
        {
            companies.Seed(id, "Plataforma", usesPlatformAssistant: true);
        }

        return companies;
    }

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
