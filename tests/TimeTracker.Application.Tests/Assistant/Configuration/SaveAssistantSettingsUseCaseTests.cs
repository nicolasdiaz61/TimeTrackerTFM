using System.Text;

using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class SaveAssistantSettingsUseCaseTests
{
    private static readonly Guid Ana = Guid.NewGuid();

    private readonly InMemorySettingsStore _store = new();
    private readonly FakeProtector _protector = new();
    private readonly FakeReloader _reloader = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private AssistantEnvironmentDefaults _env = Env(allowedProviders: "Anthropic,OpenAI,Gemini");

    private async Task<SaveAssistantSettingsResult> Save(AssistantSettingsInput input, string token = "0")
        => await new SaveAssistantSettingsUseCase(_store, _protector, new FakeEnvironment(_env), _reloader, new FixedCurrentUser(Ana, UserRole.Manager), _time, PlatformCompanies())
            .ExecuteAsync(new SaveAssistantSettingsCommand(input, token), CancellationToken.None);

    private static AssistantSettingsView Saved(SaveAssistantSettingsResult result) => Assert.IsType<SaveAssistantSettingsResult.Saved>(result).View;

    private static string[] InvalidFields(SaveAssistantSettingsResult result)
        => [.. Assert.IsType<SaveAssistantSettingsResult.Invalid>(result).Errors.Select(e => e.Field)];

    private EncryptedCredential StoreKeyFor(AssistantProvider provider, short keyVersion = 1)
        => _protector.Protect(new AssistantCredential(SecretKey), provider, Company.DefaultId) with { KeyVersion = keyVersion };

    [Fact]
    public async Task AValidSave_PersistsTheRowAuditsAndReturnsTheNewState()
    {
        var view = Saved(await Save(Input(provider: "OpenAI", model: "m-1", credential: SecretKey)));

        Assert.Equal("m-1", view.Effective.Model);
        Assert.Equal(SettingSource.Stored, view.Effective.Sources.Model);
        Assert.Equal(_store.Row!.Version, view.ConcurrencyToken);
        Assert.Equal(Ana, _store.Row.UpdatedBy);
        Assert.Equal(_time.GetUtcNow(), _store.Row.UpdatedAt);
        var audit = Assert.Single(_store.Audits);
        Assert.Equal((AuditAction.Saved, Ana, _time.GetUtcNow()), (audit.Action, audit.ActorId, audit.OccurredAt));
        Assert.Null(audit.Previous);
        Assert.Equal("m-1", audit.Current!.Model);
        Assert.Equal(1, _reloader.Calls);
    }

    [Fact]
    public async Task TheSaveInstant_IsTruncatedToMicrosecondsLikePostgres_SoThePutAndTheNextGetShowTheSameTime()
    {
        var instant = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
        _time.SetUtcNow(instant.AddTicks(1234567));

        var view = Saved(await Save(Input(provider: "OpenAI", model: "m-1", credential: SecretKey)));

        var truncated = instant.AddTicks(1234560);
        Assert.Equal(truncated, view.UpdatedAt);
        Assert.Equal(truncated, _store.Row!.UpdatedAt);
        Assert.Equal(truncated, _store.Audits.Single().OccurredAt);
    }

    [Fact]
    public async Task ANewKeyWithProviderNone_IsRejectedWithoutWritingOrAuditing()
    {
        var result = await Save(Input(provider: "None", model: null, timeoutSeconds: null, maxOutputTokens: null, credential: SecretKey));

        Assert.Equal(["credential"], InvalidFields(result));
        Assert.Null(_store.Row);
        Assert.Empty(_store.Audits);
        Assert.Equal(0, _reloader.Calls);
    }

    [Fact]
    public async Task OmittingTheKey_KeepsTheStoredOneAndDoesNotMarkItAsChanged()
    {
        _store.Row = Stored(AssistantProvider.OpenAI, credential: StoreKeyFor(AssistantProvider.OpenAI));

        var view = Saved(await Save(Input(provider: "OpenAI", credential: null), _store.Row.Version));

        Assert.True(view.Effective.CredentialConfigured);
        Assert.NotNull(_store.Row!.Credential);
        Assert.False(_store.Audits.Single().CredentialChanged);
    }

    [Fact]
    public async Task ANewKey_IsEncryptedWithTheActiveVersionAndAuditedAsChanged()
    {
        _protector.ActiveVersion = 2;

        await Save(Input(provider: "OpenAI", credential: SecretKey));

        Assert.Equal((short)2, _store.Row!.Credential!.KeyVersion);
        Assert.DoesNotContain(SecretKey, Encoding.UTF8.GetString(_store.Row.Credential.Ciphertext));
        Assert.True(_store.Audits.Single().CredentialChanged);
    }

    [Fact]
    public async Task KeepingTheKey_ReEncryptsItWithTheActiveVersion()
    {
        _store.Row = Stored(AssistantProvider.OpenAI, credential: StoreKeyFor(AssistantProvider.OpenAI, keyVersion: 1));
        _protector.ActiveVersion = 2;

        await Save(Input(provider: "OpenAI"), _store.Row.Version);

        Assert.Equal((short)2, _store.Row!.Credential!.KeyVersion);
        Assert.False(_store.Audits.Single().CredentialChanged);
    }

    [Fact]
    public async Task KeepingTheKey_WhenTheMasterKeyIsLostBeforeReEncrypting_KeepsTheStoredOneAndSaves()
    {
        var original = StoreKeyFor(AssistantProvider.OpenAI, keyVersion: 1);
        _store.Row = Stored(AssistantProvider.OpenAI, model: "viejo", credential: original);
        _protector.FailProtectAfterAvailabilityCheck = true;

        Saved(await Save(Input(provider: "OpenAI", model: "nuevo"), _store.Row.Version));

        Assert.Equal("nuevo", _store.Row!.Model);
        Assert.Same(original, _store.Row.Credential);
    }

    [Fact]
    public async Task KeepingTheKeyWithoutAProtector_LeavesItUntouched()
    {
        var original = StoreKeyFor(AssistantProvider.OpenAI, keyVersion: 1);
        _store.Row = Stored(AssistantProvider.OpenAI, credential: original);
        _protector.Available = false;

        Saved(await Save(Input(provider: "OpenAI"), _store.Row.Version));

        Assert.Same(original, _store.Row!.Credential);
    }

    [Fact]
    public async Task SwitchingProvider_DropsTheStoredKeyAndRequiresANewOne()
    {
        _env = Env(provider: AssistantProvider.Anthropic, apiKey: null, allowedProviders: "Anthropic,OpenAI,Gemini");
        _store.Row = Stored(AssistantProvider.OpenAI, credential: StoreKeyFor(AssistantProvider.OpenAI));
        var token = _store.Row.Version;

        Assert.Equal(["credential"], InvalidFields(await Save(Input(provider: "Gemini"), token)));
        Assert.Equal(0, _store.Writes);

        var view = Saved(await Save(Input(provider: "Gemini", credential: "otra-clave-654321"), token));

        Assert.True(view.Effective.CredentialConfigured);
        Assert.True(_protector.TryUnprotect(_store.Row!.Credential!, AssistantProvider.Gemini, Company.DefaultId, out var plain));
        Assert.Equal("otra-clave-654321", plain.Value);
    }

    [Fact]
    public async Task AKeptKeyThatCannotBeDecrypted_IsRejectedOnTheCredentialFieldWithoutWriting()
    {
        _env = Env(apiKey: null, allowedProviders: "Anthropic,OpenAI,Gemini");
        _store.Row = Stored(AssistantProvider.OpenAI, credential: StoreKeyFor(AssistantProvider.OpenAI, keyVersion: 1));
        _protector.RetiredVersions.Add(1);

        var result = await Save(Input(provider: "OpenAI"), _store.Row.Version);

        Assert.Equal(["credential"], InvalidFields(result));
        Assert.Equal(0, _store.Writes);
        Assert.Equal(0, _reloader.Calls);
    }

    [Fact]
    public async Task AKeptKeyThatCannotBeDecrypted_IsReplacedWhenTheRequestBringsANewOne()
    {
        _env = Env(apiKey: null, allowedProviders: "Anthropic,OpenAI,Gemini");
        _store.Row = Stored(AssistantProvider.OpenAI, credential: StoreKeyFor(AssistantProvider.OpenAI, keyVersion: 1));
        _protector.RetiredVersions.Add(1);
        _protector.ActiveVersion = 2;

        Saved(await Save(Input(provider: "OpenAI", credential: SecretKey), _store.Row.Version));

        Assert.True(_protector.TryUnprotect(_store.Row!.Credential!, AssistantProvider.OpenAI, Company.DefaultId, out _));
    }

    [Fact]
    public async Task AKeptKeyThatCannotBeDecrypted_IsAcceptedWhenTheEnvironmentHasAKeyForTheProvider()
    {
        _store.Row = Stored(AssistantProvider.Anthropic, credential: StoreKeyFor(AssistantProvider.Anthropic, keyVersion: 1));
        _protector.RetiredVersions.Add(1);

        Saved(await Save(Input(provider: "Anthropic"), _store.Row.Version));
    }

    [Fact]
    public async Task SwitchingToAProviderWithoutKey_DiscardsTheStoredKeyAndAuditsTheChange()
    {
        _env = Env(provider: AssistantProvider.Ollama, apiKey: null, hasEndpoint: true, allowedProviders: "Ollama,OpenAI");
        _store.Row = Stored(AssistantProvider.OpenAI, credential: StoreKeyFor(AssistantProvider.OpenAI));

        Saved(await Save(Input(provider: "Ollama"), _store.Row.Version));

        Assert.Null(_store.Row!.Credential);
        Assert.True(_store.Audits.Single().CredentialChanged);
    }

    [Fact]
    public async Task ANewKeyWithoutAProtector_IsRejectedOnTheCredentialFieldWithoutWriting()
    {
        _protector.Available = false;

        var result = await Save(Input(provider: "OpenAI", credential: SecretKey));

        Assert.Equal(["credential"], InvalidFields(result));
        Assert.DoesNotContain(SecretKey, string.Join(' ', ((SaveAssistantSettingsResult.Invalid)result).Errors.Select(e => e.Message)));
        Assert.Equal(0, _store.Writes);
        Assert.Equal(0, _reloader.Calls);
    }

    [Fact]
    public async Task WithoutAProtectorAndWithoutANewKey_TheSaveIsAccepted()
    {
        _protector.Available = false;

        Saved(await Save(Input(provider: "Anthropic", model: "m-2")));

        Assert.Equal("m-2", _store.Row!.Model);
        Assert.Null(_store.Row.Credential);
    }

    [Fact]
    public async Task AStaleToken_ConflictsWithoutChangingAnything()
    {
        _store.Row = Stored(model: "del-primero");

        var result = await Save(Input(), token: "99");

        Assert.IsType<SaveAssistantSettingsResult.Conflict>(result);
        Assert.Equal("del-primero", _store.Row!.Model);
        Assert.Empty(_store.Audits);
        Assert.Equal(0, _reloader.Calls);
    }

    [Fact]
    public async Task FirstCreation_ConflictsWhenSomeoneCreatedTheRowFirst()
    {
        _store.Row = Stored();

        Assert.IsType<SaveAssistantSettingsResult.Conflict>(await Save(Input(), token: "0"));
    }

    [Fact]
    public async Task TheAudit_RecordsPreviousAndCurrentValuesWithoutSecrets()
    {
        _store.Row = Stored(model: "a", credential: StoreKeyFor(AssistantProvider.Anthropic));

        await Save(Input(provider: "Anthropic", model: "b", credential: SecretKey), _store.Row.Version);

        var audit = _store.Audits.Single();
        Assert.Equal("a", audit.Previous!.Model);
        Assert.Equal("b", audit.Current!.Model);
        Assert.True(audit.CredentialChanged);
        Assert.DoesNotContain(SecretKey, audit.ToString());
    }

    [Fact]
    public async Task IfTheReloadFails_TheSaveIsStillConfirmed()
    {
        _reloader.Fails = true;

        Saved(await Save(Input()));

        Assert.NotNull(_store.Row);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    public async Task AMissingOrNonNumericToken_IsInvalid(string? token)
        => Assert.Equal(["concurrencyToken"], InvalidFields(await Save(Input(), token!)));

    [Fact]
    public async Task InvalidFields_AreReportedWithoutWriting()
    {
        var result = await Save(Input(timeoutSeconds: 4, model: "<script>"));

        Assert.Equal(["model", "timeoutSeconds"], InvalidFields(result));
        Assert.Equal(0, _store.Writes);
    }
}
