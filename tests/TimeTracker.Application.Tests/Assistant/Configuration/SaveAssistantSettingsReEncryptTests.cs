using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

/// <summary>Recifrado oportunista de v1 a v2 (DD-17): solo en el primer guardado que conserva la clave y solo en la compañía por defecto.</summary>
public class SaveAssistantSettingsReEncryptTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-7111-8111-111111111111");

    private readonly InMemorySettingsStore _store = new();
    private readonly FakeProtector _protector = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));

    private Task<SaveAssistantSettingsResult> Save(Guid companyId, AssistantSettingsInput input, string token)
        => new SaveAssistantSettingsUseCase(
                _store,
                _protector,
                new FakeEnvironment(Env(allowedProviders: "Anthropic,OpenAI")),
                new FakeReloader(),
                new FixedCurrentUser(Ana, UserRole.Manager, companyId),
                _time)
            .ExecuteAsync(new SaveAssistantSettingsCommand(input, token), CancellationToken.None);

    [Fact]
    public async Task KeepingAV1Key_InTheDefaultCompany_ReEncryptsItAsV2WithoutChangingTheSecret()
    {
        var legacy = _protector.ProtectAsLegacyV1(new AssistantCredential(SecretKey), AssistantProvider.OpenAI);
        _store.Row = Stored(AssistantProvider.OpenAI, credential: legacy, version: "7");

        var result = await Save(Company.DefaultId, Input(provider: "OpenAI", model: "m-2", credential: null), "7");

        Assert.IsType<SaveAssistantSettingsResult.Saved>(result);
        var stored = _store.Row!.Credential!;
        Assert.NotEqual(legacy.Ciphertext, stored.Ciphertext);
        Assert.Equal(_protector.Protect(new AssistantCredential(SecretKey), AssistantProvider.OpenAI, Company.DefaultId).Ciphertext, stored.Ciphertext);
        Assert.True(_protector.TryUnprotect(stored, AssistantProvider.OpenAI, Company.DefaultId, out var plain));
        Assert.Equal(SecretKey, plain!.Value);
        Assert.False(_store.Audits.Single().CredentialChanged);
    }

    [Fact]
    public async Task KeepingAV2Key_InAnotherCompany_StaysReadableOnlyForThatCompany()
    {
        var own = _protector.Protect(new AssistantCredential(SecretKey), AssistantProvider.OpenAI, CompanyA);
        _store.Row = Stored(AssistantProvider.OpenAI, credential: own, version: "7");

        await Save(CompanyA, Input(provider: "OpenAI", model: "m-2", credential: null), "7");

        var stored = _store.Row!.Credential!;
        Assert.True(_protector.TryUnprotect(stored, AssistantProvider.OpenAI, CompanyA, out var plain));
        Assert.Equal(SecretKey, plain!.Value);
        Assert.False(_protector.TryUnprotect(stored, AssistantProvider.OpenAI, Company.DefaultId, out _));
    }

    [Fact]
    public async Task AV1KeyInAnotherCompany_IsNotReadable_SoTheSaveKeepsItUntouched()
    {
        var legacy = _protector.ProtectAsLegacyV1(new AssistantCredential(SecretKey), AssistantProvider.OpenAI);
        _store.Row = Stored(AssistantProvider.OpenAI, credential: legacy, version: "7");

        // La clave guardada no es utilizable en esta compañía: sin clave nueva el guardado se rechaza por falta de clave.
        var result = await Save(CompanyA, Input(provider: "OpenAI", model: "m-2", credential: null), "7");

        Assert.Equal(["credential"], Assert.IsType<SaveAssistantSettingsResult.Invalid>(result).Errors.Select(error => error.Field));
        Assert.Equal(legacy.Ciphertext, _store.Row!.Credential!.Ciphertext);
        Assert.Equal(0, _store.Writes);
    }

    [Fact]
    public async Task WithoutAProtector_AV1KeyIsKeptAsIs()
    {
        var legacy = _protector.ProtectAsLegacyV1(new AssistantCredential(SecretKey), AssistantProvider.OpenAI);
        _store.Row = Stored(AssistantProvider.OpenAI, credential: legacy, version: "7");
        _protector.Available = false;

        var result = await Save(Company.DefaultId, Input(provider: "OpenAI", model: "m-2", credential: null), "7");

        Assert.IsType<SaveAssistantSettingsResult.Saved>(result);
        Assert.Equal(legacy.Ciphertext, _store.Row!.Credential!.Ciphertext);
    }

    [Fact]
    public async Task ReadingTheSettings_NeverRewritesTheStoredFormat()
    {
        var legacy = _protector.ProtectAsLegacyV1(new AssistantCredential(SecretKey), AssistantProvider.OpenAI);
        _store.Row = Stored(AssistantProvider.OpenAI, credential: legacy, version: "7");

        var view = await new GetAssistantSettingsUseCase(_store, _protector, new FakeEnvironment(Env(allowedProviders: "OpenAI")), new FixedCurrentUser(Ana, UserRole.Manager, Company.DefaultId))
            .ExecuteAsync(CancellationToken.None);

        Assert.True(view.Effective.CredentialConfigured);
        Assert.Equal(0, _store.Writes);
        Assert.Equal(legacy.Ciphertext, _store.Row!.Credential!.Ciphertext);
    }
}
