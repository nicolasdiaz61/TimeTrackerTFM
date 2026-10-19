using TimeTracker.Application.Assistant.Configuration;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class GetAssistantSettingsUseCaseTests
{
    private readonly InMemorySettingsStore _store = new();
    private readonly FakeProtector _protector = new();
    private AssistantEnvironmentDefaults _env = Env();

    private Task<AssistantSettingsView> Get()
        => new GetAssistantSettingsUseCase(_store, _protector, new FakeEnvironment(_env)).ExecuteAsync(CancellationToken.None);

    [Fact]
    public async Task WithoutARow_EveryValueComesFromTheEnvironmentAndTheTokenMeansNoRow()
    {
        _env = Env(model: "env-m", timeoutSeconds: 20);

        var view = await Get();

        Assert.Equal("0", view.ConcurrencyToken);
        Assert.Equal("env-m", view.Effective.Model);
        Assert.Equal(20, view.Effective.TimeoutSeconds);
        Assert.Equal(SettingSource.Env, view.Effective.Sources.Model);
        Assert.Null(view.UpdatedAt);
        Assert.Null(view.UpdatedBy);
        Assert.False(view.Applied);
        Assert.True(view.Effective.CredentialConfigured);
    }

    [Fact]
    public async Task WithARow_ExposesItsValuesSourceTokenAndAuthorship()
    {
        _store.Row = Stored(model: "m-1", version: "77");

        var view = await Get();

        Assert.Equal("m-1", view.Effective.Model);
        Assert.Equal(SettingSource.Stored, view.Effective.Sources.Model);
        Assert.Equal("77", view.ConcurrencyToken);
        Assert.Equal(_store.Row.UpdatedBy, view.UpdatedBy);
        Assert.Equal(_store.Row.UpdatedAt, view.UpdatedAt);
        Assert.True(view.Applied);
    }

    [Fact]
    public async Task ARowWhoseProviderIsNoLongerAllowed_IsNotAppliedAndTheEnvironmentShows()
    {
        _env = Env(model: "env-m", allowedProviders: "Anthropic");
        _store.Row = Stored(AssistantProvider.Gemini, model: "db-m");

        var view = await Get();

        Assert.False(view.Applied);
        Assert.Equal(AssistantProvider.Anthropic, view.Effective.Provider);
        Assert.Equal("env-m", view.Effective.Model);
        Assert.Equal(_store.Row.Version, view.ConcurrencyToken);
    }

    [Fact]
    public async Task ARowWithNone_IsAppliedEvenIfNotListed()
    {
        _env = Env(allowedProviders: "Anthropic");
        _store.Row = Stored(AssistantProvider.None, model: null);

        var view = await Get();

        Assert.True(view.Applied);
        Assert.Equal(AssistantProvider.None, view.Effective.Provider);
    }

    [Fact]
    public async Task StorageAvailability_FollowsTheProtector()
    {
        Assert.True((await Get()).CredentialStorageAvailable);

        _protector.Available = false;

        Assert.False((await Get()).CredentialStorageAvailable);
    }

    [Fact]
    public async Task AStoredKey_ReportsItAsConfiguredWithoutExposingIt()
    {
        _env = Env(apiKey: null);
        _store.Row = Stored(credential: _protector.Protect(new AssistantCredential(SecretKey), AssistantProvider.Anthropic));

        var view = await Get();

        Assert.True(view.Effective.CredentialConfigured);
        Assert.Equal(SettingSource.Stored, view.Effective.Sources.Credential);
        Assert.DoesNotContain(SecretKey, view.ToString());
    }

    [Fact]
    public async Task WithoutAnyKey_TheCredentialSourceIsNone()
    {
        _env = Env(apiKey: null);
        _store.Row = Stored(credential: null);

        var view = await Get();

        Assert.False(view.Effective.CredentialConfigured);
        Assert.Equal(SettingSource.None, view.Effective.Sources.Credential);
    }

    [Fact]
    public async Task AStoredKeyThatCannotBeDecrypted_DoesNotCountAsConfigured()
    {
        _env = Env(apiKey: null);
        _store.Row = Stored(credential: _protector.Protect(new AssistantCredential(SecretKey), AssistantProvider.Anthropic));
        _protector.RetiredVersions.Add(1);

        Assert.False((await Get()).Effective.CredentialConfigured);
    }

    [Fact]
    public async Task AllowedProviders_AreObjectsWithTheirCapabilities()
    {
        _env = Env(allowedProviders: "Anthropic,Ollama", hasEndpoint: true);

        var view = await Get();

        Assert.Equal(
            [
                new AllowedProviderInfo(AssistantProvider.Anthropic, RequiresCredential: true, SupportsEffort: true),
                new AllowedProviderInfo(AssistantProvider.Ollama, RequiresCredential: false, SupportsEffort: true),
            ],
            view.AllowedProviders);
    }
}
