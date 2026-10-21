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

        var result = AssistantSettingsResolver.Resolve(env, null, storedCredentialUsable: false, platformDefaults: true);

        Assert.Equal(new EffectiveAssistantSettings(
            AssistantProvider.Anthropic, "env-m", 20, 500, AssistantEffort.Low, CredentialConfigured: true,
            new EffectiveSources(SettingSource.Env, SettingSource.Env, SettingSource.Env, SettingSource.Env, SettingSource.Env, SettingSource.Env)), result);
    }

    [Fact]
    public void AStoredValue_WinsOverTheEnvironmentValueByValue()
    {
        var stored = Stored(model: "db-m", timeoutSeconds: 40);

        var result = AssistantSettingsResolver.Resolve(Env(model: "env-m", timeoutSeconds: 20, maxOutputTokens: 500), stored, false, platformDefaults: true);

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
        => Assert.Equal(AssistantEffort.High, AssistantSettingsResolver.Resolve(Env(effort: AssistantEffort.Low), Stored(effort: AssistantEffort.High), false, platformDefaults: true).Effort);

    [Fact]
    public void AStoredNone_DisablesTheAssistantEvenWithAnEnvironmentProvider()
    {
        var result = AssistantSettingsResolver.Resolve(Env(), Stored(AssistantProvider.None), false, platformDefaults: true);

        Assert.Equal(AssistantProvider.None, result.Provider);
        Assert.False(result.CredentialConfigured);
        Assert.Equal(SettingSource.None, result.Sources.Credential);
    }

    [Fact]
    public void TheEnvironmentKey_OnlyAppliesToTheEnvironmentProvider()
    {
        var env = Env(provider: AssistantProvider.Anthropic, apiKey: SecretKey);

        var other = AssistantSettingsResolver.Resolve(env, Stored(AssistantProvider.Gemini), false, platformDefaults: true);
        var same = AssistantSettingsResolver.Resolve(env, Stored(AssistantProvider.Anthropic), false, platformDefaults: true);

        Assert.False(other.CredentialConfigured);
        Assert.Equal(SettingSource.None, other.Sources.Credential);
        Assert.True(same.CredentialConfigured);
        Assert.Equal(SettingSource.Env, same.Sources.Credential);
    }

    [Fact]
    public void AUsableStoredKey_WinsOverTheEnvironmentKey()
    {
        var result = AssistantSettingsResolver.Resolve(Env(), Stored(credential: SomeKey), storedCredentialUsable: true, platformDefaults: true);

        Assert.Equal(SettingSource.Stored, result.Sources.Credential);
        Assert.True(result.CredentialConfigured);
    }

    [Fact]
    public void AnUnusableStoredKey_FallsBackToTheEnvironmentKeyOfTheSameProvider()
    {
        var result = AssistantSettingsResolver.Resolve(Env(), Stored(credential: SomeKey), storedCredentialUsable: false, platformDefaults: true);

        Assert.Equal(SettingSource.Env, result.Sources.Credential);
    }

    [Fact]
    public void SwitchingProvider_DoesNotInheritTheEnvironmentKey()
    {
        var result = AssistantSettingsResolver.Resolve(Env(provider: AssistantProvider.Anthropic), Stored(AssistantProvider.OpenAI, credential: null), false, platformDefaults: true);

        Assert.False(result.CredentialConfigured);
    }

    [Fact]
    public void WithoutARow_AndNotPlatform_TheAssistantIsDisabledWithDefaultOrigins()
    {
        var env = Env(provider: AssistantProvider.Anthropic, model: "env-m", timeoutSeconds: 20, maxOutputTokens: 500, apiKey: SecretKey);

        var result = AssistantSettingsResolver.Resolve(env, null, storedCredentialUsable: false, platformDefaults: false);

        Assert.Equal(AssistantProvider.None, result.Provider);
        Assert.Null(result.Model);
        Assert.False(result.CredentialConfigured);
        Assert.Equal(20, result.TimeoutSeconds);
        Assert.Equal(500, result.MaxOutputTokens);
        Assert.Equal(SettingSource.Default, result.Sources.Provider);
        Assert.Equal(SettingSource.Default, result.Sources.Model);
        Assert.Equal(SettingSource.None, result.Sources.Credential);
        Assert.Equal(SettingSource.Env, result.Sources.TimeoutSeconds);
        Assert.Equal(SettingSource.Env, result.Sources.MaxOutputTokens);
    }

    [Fact]
    public void WithoutARow_AndNotPlatform_NothingFromTheEnvironmentLeaks_EvenWhenTheEnvironmentIsFullyConfigured()
    {
        var plain = AssistantSettingsResolver.Resolve(Env(effort: AssistantEffort.High), null, false, platformDefaults: false);

        Assert.Null(plain.Effort);
        Assert.Equal(SettingSource.Default, plain.Sources.Effort);
    }

    [Fact]
    public void WithoutARow_AndPlatform_IsStillTheEnvironmentOfToday()
    {
        var result = AssistantSettingsResolver.Resolve(Env(model: "env-m"), null, storedCredentialUsable: false, platformDefaults: true);

        Assert.Equal(AssistantProvider.Anthropic, result.Provider);
        Assert.Equal("env-m", result.Model);
        Assert.True(result.CredentialConfigured);
        Assert.Equal(SettingSource.Env, result.Sources.Provider);
    }

    [Fact]
    public void ARow_OfANonPlatformCompany_NeverUsesTheEnvironmentKey_EvenForTheEnvironmentProvider()
    {
        var env = Env(provider: AssistantProvider.Anthropic, apiKey: SecretKey);

        var withoutKey = AssistantSettingsResolver.Resolve(env, Stored(AssistantProvider.Anthropic), storedCredentialUsable: false, platformDefaults: false);
        var withOwnKey = AssistantSettingsResolver.Resolve(env, Stored(AssistantProvider.Anthropic, credential: SomeKey), storedCredentialUsable: true, platformDefaults: false);

        Assert.False(withoutKey.CredentialConfigured);
        Assert.Equal(SettingSource.None, withoutKey.Sources.Credential);
        Assert.True(withOwnKey.CredentialConfigured);
        Assert.Equal(SettingSource.Stored, withOwnKey.Sources.Credential);
    }

    [Fact]
    public void ARow_OfANonPlatformCompany_TakesTheModelOnlyFromTheRow()
    {
        var env = Env(model: "env-m", timeoutSeconds: 20);

        var withoutModel = AssistantSettingsResolver.Resolve(env, Stored(model: null, timeoutSeconds: null), false, platformDefaults: false);
        var withModel = AssistantSettingsResolver.Resolve(env, Stored(model: "db-m"), false, platformDefaults: false);

        Assert.Null(withoutModel.Model);
        Assert.Equal(SettingSource.Default, withoutModel.Sources.Model);
        Assert.Equal(20, withoutModel.TimeoutSeconds);
        Assert.Equal(SettingSource.Env, withoutModel.Sources.TimeoutSeconds);
        Assert.Equal("db-m", withModel.Model);
        Assert.Equal(SettingSource.Stored, withModel.Sources.Model);
    }

    [Fact]
    public void NoRow_AndAStoredNone_AreIndistinguishableForTheChat()
    {
        var noRow = AssistantSettingsResolver.Resolve(Env(), null, false, platformDefaults: false);
        var storedNone = AssistantSettingsResolver.Resolve(Env(), Stored(AssistantProvider.None, model: null), false, platformDefaults: false);

        Assert.Equal(noRow.Provider, storedNone.Provider);
        Assert.Equal(noRow.CredentialConfigured, storedNone.CredentialConfigured);
        Assert.Null(storedNone.Model);
    }
}
