using TimeTracker.Application.Assistant.Configuration;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class AssistantSettingsResolverTests
{
    private static readonly EncryptedCredential SomeKey = new([1, 2, 3], new byte[12], 1);

    [Fact]
    public void WithoutARow_TheResultIsExactlyTheEnvironment()
    {
        var env = Env(model: "env-m", timeoutSeconds: 20, maxOutputTokens: 500, effort: AssistantEffort.Low);

        var result = AssistantSettingsResolver.Resolve(env, null, storedCredentialUsable: false);

        Assert.Equal(new EffectiveAssistantSettings(
            AssistantProvider.Anthropic, "env-m", 20, 500, AssistantEffort.Low, CredentialConfigured: true,
            new EffectiveSources(SettingSource.Env, SettingSource.Env, SettingSource.Env, SettingSource.Env, SettingSource.Env, SettingSource.Env)), result);
    }

    [Fact]
    public void AStoredValue_WinsOverTheEnvironmentValueByValue()
    {
        var stored = Stored(model: "db-m", timeoutSeconds: 40);

        var result = AssistantSettingsResolver.Resolve(Env(model: "env-m", timeoutSeconds: 20, maxOutputTokens: 500), stored, false);

        Assert.Equal("db-m", result.Model);
        Assert.Equal(40, result.TimeoutSeconds);
        Assert.Equal(500, result.MaxOutputTokens);
        Assert.Equal(SettingSource.Stored, result.Sources.Model);
        Assert.Equal(SettingSource.Stored, result.Sources.TimeoutSeconds);
        Assert.Equal(SettingSource.Env, result.Sources.MaxOutputTokens);
        Assert.Equal(SettingSource.Stored, result.Sources.Provider);
    }

    [Fact]
    public void AStoredEffort_WinsOverTheEnvironmentEffort()
        => Assert.Equal(AssistantEffort.High, AssistantSettingsResolver.Resolve(Env(effort: AssistantEffort.Low), Stored(effort: AssistantEffort.High), false).Effort);

    [Fact]
    public void AStoredNone_DisablesTheAssistantEvenWithAnEnvironmentProvider()
    {
        var result = AssistantSettingsResolver.Resolve(Env(), Stored(AssistantProvider.None), false);

        Assert.Equal(AssistantProvider.None, result.Provider);
        Assert.False(result.CredentialConfigured);
        Assert.Equal(SettingSource.None, result.Sources.Credential);
    }

    [Fact]
    public void TheEnvironmentKey_OnlyAppliesToTheEnvironmentProvider()
    {
        var env = Env(provider: AssistantProvider.Anthropic, apiKey: SecretKey);

        var other = AssistantSettingsResolver.Resolve(env, Stored(AssistantProvider.Gemini), false);
        var same = AssistantSettingsResolver.Resolve(env, Stored(AssistantProvider.Anthropic), false);

        Assert.False(other.CredentialConfigured);
        Assert.Equal(SettingSource.None, other.Sources.Credential);
        Assert.True(same.CredentialConfigured);
        Assert.Equal(SettingSource.Env, same.Sources.Credential);
    }

    [Fact]
    public void AUsableStoredKey_WinsOverTheEnvironmentKey()
    {
        var result = AssistantSettingsResolver.Resolve(Env(), Stored(credential: SomeKey), storedCredentialUsable: true);

        Assert.Equal(SettingSource.Stored, result.Sources.Credential);
        Assert.True(result.CredentialConfigured);
    }

    [Fact]
    public void AnUnusableStoredKey_FallsBackToTheEnvironmentKeyOfTheSameProvider()
    {
        var result = AssistantSettingsResolver.Resolve(Env(), Stored(credential: SomeKey), storedCredentialUsable: false);

        Assert.Equal(SettingSource.Env, result.Sources.Credential);
    }

    [Fact]
    public void SwitchingProvider_DoesNotInheritTheEnvironmentKey()
    {
        var result = AssistantSettingsResolver.Resolve(Env(provider: AssistantProvider.Anthropic), Stored(AssistantProvider.OpenAI, credential: null), false);

        Assert.False(result.CredentialConfigured);
    }
}
