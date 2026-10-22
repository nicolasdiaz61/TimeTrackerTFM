using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>
/// TC-10 (DD-14): el registro de instantáneas por compañía. Reloj falso y lector en memoria: ninguna espera real, ninguna base de
/// datos, ningún proveedor. La compañía de plataforma (indicador verdadero) es la única que puede usar la clave del entorno.
/// </summary>
public sealed class AssistantRuntimeRegistryTests : IDisposable
{
    private const string PlatformSecret = "clave-plataforma-123456";
    private const string OwnSecret = "clave-prueba-123456";

    private static readonly Guid CompanyA = Guid.Parse("00000000-0000-4000-8000-0000000000a1");
    private static readonly Guid CompanyB = Guid.Parse("00000000-0000-4000-8000-0000000000b1");
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private static AiOptions PlatformEnvironment(int maxLoaded = 100, int idleMinutes = 60)
        => new()
        {
            Provider = "Anthropic",
            Model = "env-m",
            ApiKey = PlatformSecret,
            AllowedProviders = "Ollama,Anthropic,OpenAI",
            Endpoint = "http://localhost:11434/",
            MaxLoadedCompanies = maxLoaded,
            RuntimeIdleMinutes = idleMinutes,
        };

    private static AesGcmCredentialProtector Protector()
    {
        var options = new AiOptions { ActiveMasterKeyVersion = "1" };
        options.MasterKeys["1"] = Key;
        return new AesGcmCredentialProtector(Options.Create(options));
    }

    private static AssistantSettings Row(
        AssistantProvider provider,
        string version,
        string? model = "db-m",
        EncryptedCredential? credential = null,
        int? timeoutSeconds = null,
        int? maxOutputTokens = null,
        AssistantEffort? effort = null)
        => new(provider, model, timeoutSeconds, maxOutputTokens, effort, credential, Guid.NewGuid(), DateTimeOffset.UnixEpoch, version);

    private static EncryptedCredential Encrypt(AesGcmCredentialProtector protector, AssistantProvider provider, Guid company)
        => protector.Protect(new AssistantCredential(OwnSecret), provider, company);

    private Rig NewRig(AiOptions? environment = null, AesGcmCredentialProtector? protector = null)
    {
        var rig = new Rig(environment ?? PlatformEnvironment(), protector);
        _disposables.Add(rig.Registry);
        return rig;
    }

    private sealed class Rig
    {
        public Rig(AiOptions environment, AesGcmCredentialProtector? protector = null)
        {
            Protector = protector ?? AssistantRuntimeRegistryTests.Protector();
            var options = Options.Create(environment);
            Registry = new AssistantRuntimeRegistry(
                Reader, new AssistantOptionsResolver(options, new ConfigurationAssistantEnvironment(options), Protector), Factory, options, Time, Logger);
        }

        public FakeCompanySettingsReader Reader { get; } = new();

        public RecordingChatClientFactory Factory { get; } = new();

        public CapturingLogger<AssistantRuntimeRegistry> Logger { get; } = new();

        public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));

        public AesGcmCredentialProtector Protector { get; }

        public AssistantRuntimeRegistry Registry { get; }

        public Task<AssistantSnapshot> GetAsync(Guid company) => Registry.GetAsync(company, CancellationToken.None).AsTask();
    }

    // --- Carga única, instantánea por compañía ---

    [Fact]
    public async Task ManyConcurrentQuestionsOfOneCompany_LoadItsRowExactlyOnce()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        rig.Reader.Gate = new TaskCompletionSource();

        var calls = Enumerable.Range(0, 20).Select(_ => Task.Run(() => rig.GetAsync(CompanyA))).ToArray();
        await Task.Delay(50);
        rig.Reader.Gate.SetResult();
        var snapshots = await Task.WhenAll(calls);

        Assert.Equal(1, rig.Reader.Reads[CompanyA]);
        Assert.Single(rig.Factory.Created);
        Assert.All(snapshots, snapshot => Assert.Same(snapshots[0], snapshot));
        Assert.Equal("7", snapshots[0].Version);
        Assert.NotNull(snapshots[0].Pipeline);
    }

    [Fact]
    public async Task EachCompanyGetsItsOwnSnapshot_AndNeverTheOneOfAnother()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7", model: "modelo-de-a"), usesPlatform: false);
        rig.Reader.Set(CompanyB, Row(AssistantProvider.Ollama, "9", model: "modelo-de-b"), usesPlatform: false);

        var a = await rig.GetAsync(CompanyA);
        var b = await rig.GetAsync(CompanyB);

        Assert.Equal(("modelo-de-a", "7"), (a.Options.Model, a.Version));
        Assert.Equal(("modelo-de-b", "9"), (b.Options.Model, b.Version));
        Assert.NotSame(a, b);
        Assert.Equal(2, rig.Factory.Created.Count);
        Assert.Equal(["modelo-de-a", "modelo-de-b"], rig.Factory.Created.Select(created => created.Model));
    }

    [Fact]
    public async Task ACompanyWithOwnKeyUsesItAndNeverThePlatformKey()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Anthropic, "7", credential: Encrypt(rig.Protector, AssistantProvider.Anthropic, CompanyA)), usesPlatform: false);

        await rig.GetAsync(CompanyA);

        var created = Assert.Single(rig.Factory.Created);
        Assert.Equal(OwnSecret, created.ApiKey);
        Assert.NotEqual(PlatformSecret, created.ApiKey);
    }

    [Fact]
    public async Task ACompanyThatIsNotPlatform_WithoutARow_IsDisabledWithoutPipelineOrFactoryCall()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, stored: null, usesPlatform: false);

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Null(snapshot.Pipeline);
        Assert.Equal("0", snapshot.Version);
        Assert.Empty(rig.Factory.Created);
    }

    [Fact]
    public async Task ACompanyThatIsNotPlatform_WithARowWithoutKey_NeverInheritsThePlatformKey()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Anthropic, "7", model: "db-m", credential: null), usesPlatform: false);

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Null(snapshot.Pipeline);
        Assert.Empty(rig.Factory.Created);
    }

    [Fact]
    public async Task ThePlatformCompanyWithoutARow_UsesTheEnvironmentIncludingItsKey()
    {
        var rig = NewRig();
        rig.Reader.Set(Company.DefaultId, stored: null, usesPlatform: true);

        var snapshot = await rig.GetAsync(Company.DefaultId);

        Assert.NotNull(snapshot.Pipeline);
        Assert.Equal("0", snapshot.Version);
        var created = Assert.Single(rig.Factory.Created);
        Assert.Equal((PlatformSecret, "env-m"), (created.ApiKey, created.Model));
        Assert.Null(snapshot.Options.ApiKey);
    }

    // --- Primera fila inválida: deshabilitado, nunca la plataforma ---

    [Fact]
    public async Task TheFirstRowIsInvalid_LeavesTheCompanyDisabled_CachedForThatVersion_AndNeverFallsBackToThePlatform()
    {
        var rig = NewRig();
        // Proveedor fuera de la lista permitida: inválida aunque la compañía sea la de plataforma (la demo).
        rig.Reader.Set(Company.DefaultId, Row(AssistantProvider.Gemini, "21"), usesPlatform: true);

        var first = await rig.GetAsync(Company.DefaultId);
        var second = await rig.GetAsync(Company.DefaultId);

        Assert.Null(first.Pipeline);
        Assert.Equal("21", first.Version);
        Assert.Same(first, second);
        Assert.Equal(1, rig.Reader.Reads[Company.DefaultId]);
        Assert.Empty(rig.Factory.Created);
        var warning = Assert.Single(rig.Logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("21", warning.Message, StringComparison.Ordinal);
        Assert.Equal("21", Assert.Single(rig.Registry.Loaded()).LastRejectedVersion);
    }

    [Fact]
    public async Task TheFirstRowIsUndecryptable_LeavesTheCompanyDisabled_WithoutLeakingSecrets()
    {
        var rig = NewRig();
        var encrypted = Encrypt(rig.Protector, AssistantProvider.Anthropic, CompanyA);
        encrypted.Ciphertext[0] ^= 0x01;
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Anthropic, "30", credential: encrypted), usesPlatform: false);

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Null(snapshot.Pipeline);
        Assert.Empty(rig.Factory.Created);
        var warning = Assert.Single(rig.Logger.Entries);
        Assert.Contains("undecryptable", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnSecret, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFirstRowHasAProviderThatIsNotAllowed_LeavesTheCompanyDisabled()
    {
        var rig = NewRig(PlatformEnvironment());
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Gemini, "40", credential: Encrypt(rig.Protector, AssistantProvider.Gemini, CompanyA)), usesPlatform: false);

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Null(snapshot.Pipeline);
        Assert.Contains("not-allowed", Assert.Single(rig.Logger.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenTheFactoryFailsOnTheFirstRow_TheCompanyIsDisabled_AndOnlyTheExceptionTypeIsLogged()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "50"), usesPlatform: false);
        rig.Factory.Failure = new InvalidOperationException($"fallo con {OwnSecret}");

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Null(snapshot.Pipeline);
        var warning = Assert.Single(rig.Logger.Entries);
        Assert.Contains(nameof(InvalidOperationException), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnSecret, warning.Message, StringComparison.Ordinal);
    }

    // --- Fallos transitorios: no se cachean ---

    [Fact]
    public async Task ATransientReadFailure_IsNotCached_AndTheNextQuestionRetriesAndBuildsTheSnapshot()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        rig.Reader.FailNextRead(new TimeoutException("base de datos caída"));

        await Assert.ThrowsAsync<TimeoutException>(() => rig.GetAsync(CompanyA));
        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Equal(2, rig.Reader.Reads[CompanyA]);
        Assert.NotNull(snapshot.Pipeline);
        Assert.Equal("7", snapshot.Version);
    }

    [Fact]
    public async Task ALoadThatTimesOut_IsNotCachedEither()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        rig.Reader.Gate = new TaskCompletionSource();

        var stuck = rig.GetAsync(CompanyA);
        rig.Time.Advance(AssistantRuntimeRegistry.LoadTimeout + TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stuck);

        rig.Reader.Gate = null;
        var snapshot = await rig.GetAsync(CompanyA);

        Assert.NotNull(snapshot.Pipeline);
        Assert.Equal(2, rig.Reader.Reads[CompanyA]);
    }

    [Fact]
    public async Task ACallerThatCancels_DoesNotCancelTheLoadOtherCallersWaitFor()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        rig.Reader.Gate = new TaskCompletionSource();
        using var cancellation = new CancellationTokenSource();

        var impatient = rig.Registry.GetAsync(CompanyA, cancellation.Token).AsTask();
        var patient = rig.GetAsync(CompanyA);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => impatient);
        rig.Reader.Gate.SetResult();

        Assert.NotNull((await patient).Pipeline);
        Assert.Equal(1, rig.Reader.Reads[CompanyA]);
    }

    // --- Recarga: Swap por versión, gracia de 120 s, conservar la última válida ---

    [Fact]
    public async Task ReloadWithANewVersion_SwapsTheSnapshot_AndTheInFlightOneSurvivesTheGracePeriod()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7", model: "viejo"), usesPlatform: false);
        var inFlight = await rig.GetAsync(CompanyA);
        var oldClient = rig.Factory.Clients[0];
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "8", model: "nuevo"), usesPlatform: false);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        var fresh = await rig.GetAsync(CompanyA);

        Assert.Equal(("nuevo", "8"), (fresh.Options.Model, fresh.Version));
        Assert.Equal("7", inFlight.Version);
        rig.Time.Advance(TimeSpan.FromSeconds(119));
        Assert.Equal(0, oldClient.DisposeCount);
        rig.Time.Advance(TimeSpan.FromSeconds(2));
        Assert.True(oldClient.DisposeCount > 0);
    }

    [Fact]
    public async Task ReloadWithTheSameVersion_DoesNotRebuildAnything()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        await rig.GetAsync(CompanyA);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        Assert.Single(rig.Factory.Created);
    }

    [Fact]
    public async Task ReloadOfACompanyThatIsNotLoaded_ReadsNothing()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyB, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);

        await rig.Registry.ReloadAsync(CompanyB, CancellationToken.None);

        Assert.Equal(0, rig.Reader.TotalReads);
        Assert.Empty(rig.Registry.Loaded());
    }

    [Fact]
    public async Task ReloadingA_NeverRebuildsB()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7", model: "a1"), usesPlatform: false);
        rig.Reader.Set(CompanyB, Row(AssistantProvider.Ollama, "9", model: "b1"), usesPlatform: false);
        await rig.GetAsync(CompanyA);
        var before = await rig.GetAsync(CompanyB);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "8", model: "a2"), usesPlatform: false);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        Assert.Same(before, await rig.GetAsync(CompanyB));
        Assert.Equal(1, rig.Reader.Reads[CompanyB]);
    }

    [Fact]
    public async Task ALaterInvalidRow_KeepsTheLastValidOne_AndWarnsOncePerRejectedVersion()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7", model: "bueno"), usesPlatform: false);
        var before = await rig.GetAsync(CompanyA);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Anthropic, "8", model: null), usesPlatform: false);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        Assert.Same(before, await rig.GetAsync(CompanyA));
        var warning = Assert.Single(rig.Logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("8", warning.Message, StringComparison.Ordinal);
        Assert.Equal("8", Assert.Single(rig.Registry.Loaded()).LastRejectedVersion);

        rig.Reader.Set(CompanyA, Row(AssistantProvider.Anthropic, "9", model: null), usesPlatform: false);
        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        Assert.Equal(2, rig.Logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task AValidRowAfterARejectedOne_ClearsTheRejectedVersion()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        await rig.GetAsync(CompanyA);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Anthropic, "8", model: null), usesPlatform: false);
        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "9"), usesPlatform: false);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        var info = Assert.Single(rig.Registry.Loaded());
        Assert.Null(info.LastRejectedVersion);
        Assert.Equal("9", info.Version);
    }

    [Fact]
    public async Task ARowDeletedAfterAStoredOne_RestoresThePlatformEnvironmentUnderVersionZero()
    {
        var rig = NewRig();
        rig.Reader.Set(Company.DefaultId, Row(AssistantProvider.Ollama, "7"), usesPlatform: true);
        await rig.GetAsync(Company.DefaultId);
        rig.Reader.Set(Company.DefaultId, stored: null, usesPlatform: true);

        await rig.Registry.ReloadAsync(Company.DefaultId, CancellationToken.None);

        var snapshot = await rig.GetAsync(Company.DefaultId);
        Assert.Equal(("0", "env-m"), (snapshot.Version, snapshot.Options.Model));
    }

    // --- Aviso de la revisión de la 4a: el indicador de plataforma cuenta ---

    [Fact]
    public async Task WhenThePlatformIndicatorIsLost_TheEnvironmentSnapshotIsReplacedByADisabledOne()
    {
        var rig = NewRig();
        rig.Reader.Set(Company.DefaultId, stored: null, usesPlatform: true);
        Assert.NotNull((await rig.GetAsync(Company.DefaultId)).Pipeline);
        rig.Reader.Set(Company.DefaultId, stored: null, usesPlatform: false);

        await rig.Registry.ReloadAsync(Company.DefaultId, CancellationToken.None);

        var snapshot = await rig.GetAsync(Company.DefaultId);
        Assert.Null(snapshot.Pipeline);
        Assert.False(Assert.Single(rig.Registry.Loaded()).UsesPlatform);
    }

    [Fact]
    public async Task WhenThePlatformIndicatorIsLost_AndTheRowIsNowRejected_TheCompanyDegradesToDisabledInsteadOfKeepingThePlatformKey()
    {
        var rig = NewRig();
        // Fila válida solo porque hereda la clave de plataforma del entorno (mismo proveedor, sin clave propia).
        rig.Reader.Set(Company.DefaultId, Row(AssistantProvider.Anthropic, "7", model: "db-m"), usesPlatform: true);
        Assert.NotNull((await rig.GetAsync(Company.DefaultId)).Pipeline);
        Assert.Equal(PlatformSecret, rig.Factory.Created[0].ApiKey);
        rig.Reader.Set(Company.DefaultId, Row(AssistantProvider.Anthropic, "7", model: "db-m"), usesPlatform: false);

        await rig.Registry.ReloadAsync(Company.DefaultId, CancellationToken.None);

        Assert.Null((await rig.GetAsync(Company.DefaultId)).Pipeline);
        Assert.Single(rig.Factory.Created);
    }

    [Fact]
    public async Task WhenThePlatformIndicatorIsRegained_TheEnvironmentSnapshotIsBuiltAgain()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, stored: null, usesPlatform: false);
        Assert.Null((await rig.GetAsync(CompanyA)).Pipeline);
        rig.Reader.Set(CompanyA, stored: null, usesPlatform: true);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        Assert.NotNull((await rig.GetAsync(CompanyA)).Pipeline);
        Assert.True(Assert.Single(rig.Registry.Loaded()).UsesPlatform);
    }

    [Fact]
    public async Task ALoadedCompanyIsReportedWithItsVersionAndIndicator()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        rig.Reader.Set(CompanyB, stored: null, usesPlatform: true);
        await rig.GetAsync(CompanyA);
        await rig.GetAsync(CompanyB);

        var loaded = rig.Registry.Loaded().OrderBy(info => info.CompanyId).ToList();

        Assert.Equal(
            [new LoadedRuntime(CompanyA, "7", false, null), new LoadedRuntime(CompanyB, "0", true, null)],
            loaded);
    }

    // --- Recarga perpetua: ganar el indicador de plataforma con una fila rechazada ---

    [Fact]
    public async Task WhenThePlatformIndicatorIsGained_AndTheRowIsRejected_TheIndicatorIsRecorded_SoThePollerSeesNoChange()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        var before = await rig.GetAsync(CompanyA);
        // Proveedor fuera de la lista permitida: rechazada también para una compañía de plataforma.
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Gemini, "8"), usesPlatform: true);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        var loaded = Assert.Single(rig.Registry.Loaded());
        Assert.Equal(new LoadedRuntime(CompanyA, "7", true, "8"), loaded);
        Assert.Same(before, await rig.GetAsync(CompanyA));
        Assert.Single(rig.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task AValidRowStillReloads_AfterTheIndicatorWasGainedWithARejectedRow()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        await rig.GetAsync(CompanyA);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Gemini, "8"), usesPlatform: true);
        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "9", model: "nuevo"), usesPlatform: true);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        var snapshot = await rig.GetAsync(CompanyA);
        Assert.Equal(("nuevo", "9"), (snapshot.Options.Model, snapshot.Version));
        Assert.Null(Assert.Single(rig.Registry.Loaded()).LastRejectedVersion);
    }

    [Fact]
    public async Task LosingTheIndicatorAgain_AfterGainingItWithARejectedRow_KeepsTheLastValidOneBuiltWithoutThePlatformKey()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        var before = await rig.GetAsync(CompanyA);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Gemini, "8"), usesPlatform: true);
        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Gemini, "8"), usesPlatform: false);

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        // Esa configuración nunca usó la clave de plataforma: no hay motivo para degradarla.
        Assert.Same(before, await rig.GetAsync(CompanyA));
        Assert.False(Assert.Single(rig.Registry.Loaded()).UsesPlatform);
    }

    // --- Un fallo de carga tras abandonar la espera no deja una entrada fallida ---

    [Fact]
    public async Task ALoadThatFailsAfterItsOnlyCallerGaveUp_DoesNotLeaveAFailedEntry_ForTheNextQuestion()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        rig.Reader.Gate = new TaskCompletionSource();
        rig.Reader.Failure = new TimeoutException("base de datos caída");
        using var cancellation = new CancellationTokenSource();

        // Como la precarga del arranque: nadie más espera esta carga y quien la pidió se rinde.
        var preload = rig.Registry.GetAsync(CompanyA, cancellation.Token).AsTask();
        await rig.Reader.WaitForReadsAsync(1);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preload);
        rig.Reader.Gate.SetResult();
        Assert.True(SpinWait.SpinUntil(() => rig.Registry.EntryCount == 0, TimeSpan.FromSeconds(10)));
        rig.Reader.Failure = null;
        rig.Reader.Gate = null;

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.NotNull(snapshot.Pipeline);
        Assert.Equal(2, rig.Reader.Reads[CompanyA]);
    }

    // --- Escenarios del antiguo cargador, ahora sobre el registro ---

    [Fact]
    public async Task AValidRow_MapsProviderModelKeyTimeoutTokensEffortAndEndpoint_AndKeepsTheKeyOutOfTheSnapshot()
    {
        var rig = NewRig();
        var credential = Encrypt(rig.Protector, AssistantProvider.Anthropic, CompanyA);
        rig.Reader.Set(
            CompanyA,
            Row(AssistantProvider.Anthropic, "17", model: "db-m", credential: credential, timeoutSeconds: 40, maxOutputTokens: 600, effort: AssistantEffort.High),
            usesPlatform: false);

        var snapshot = await rig.GetAsync(CompanyA);

        var created = Assert.Single(rig.Factory.Created);
        Assert.Equal(("Anthropic", "db-m", OwnSecret, 40, 600), (created.Provider, created.Model, created.ApiKey, created.TimeoutSeconds, created.MaxOutputTokens));
        Assert.Equal(AssistantEffort.High, created.ResolvedEffort);
        Assert.Equal("http://localhost:11434/", created.Endpoint);
        Assert.Equal("17", snapshot.Version);
        Assert.Null(snapshot.Options.ApiKey);
        Assert.DoesNotContain(rig.Logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task APlatformCompanyRowThatOmitsValues_TakesThemFromTheEnvironment()
    {
        var environment = PlatformEnvironment();
        environment.TimeoutSeconds = 33;
        var rig = NewRig(environment);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "3", model: null), usesPlatform: true);

        await rig.GetAsync(CompanyA);

        var created = Assert.Single(rig.Factory.Created);
        Assert.Equal(("Ollama", "env-m", 33), (created.Provider, created.Model, created.TimeoutSeconds));
    }

    [Fact]
    public async Task TheNoneProvider_DisablesTheCompanyUnderTheRowVersion_WithoutBuildingAClient()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.None, "9", model: null), usesPlatform: false);

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Null(snapshot.Pipeline);
        Assert.Equal("9", snapshot.Version);
        Assert.Empty(rig.Factory.Clients);
        Assert.DoesNotContain(rig.Logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ARowEncryptedWithARetiredMasterKey_IsRejectedAsUndecryptable_AndNeverUsed()
    {
        var writer = Protector();
        var otherKey = new AiOptions { ActiveMasterKeyVersion = "2" };
        otherKey.MasterKeys["2"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var rig = NewRig(protector: new AesGcmCredentialProtector(Options.Create(otherKey)));
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Anthropic, "31", credential: Encrypt(writer, AssistantProvider.Anthropic, CompanyA)), usesPlatform: false);

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Null(snapshot.Pipeline);
        Assert.Empty(rig.Factory.Created);
        var warning = Assert.Single(rig.Logger.Entries);
        Assert.Contains("undecryptable", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnSecret, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAMasterKey_TheStoredCredentialIsNotUsed()
    {
        var writer = Protector();
        var rig = NewRig(protector: new AesGcmCredentialProtector(Options.Create(new AiOptions())));
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Anthropic, "32", credential: Encrypt(writer, AssistantProvider.Anthropic, CompanyA)), usesPlatform: false);

        var snapshot = await rig.GetAsync(CompanyA);

        Assert.Null(snapshot.Pipeline);
        Assert.Empty(rig.Factory.Created);
        Assert.Equal("32", snapshot.Version);
    }

    [Fact]
    public async Task WhenTheFactoryFailsOnAReload_TheLastValidOneIsKept_AndOnlyTheExceptionTypeIsLogged()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        var before = await rig.GetAsync(CompanyA);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "8"), usesPlatform: false);
        rig.Factory.Failure = new InvalidOperationException($"fallo con {OwnSecret}");

        await rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);

        Assert.Same(before, await rig.GetAsync(CompanyA));
        var warning = Assert.Single(rig.Logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(nameof(InvalidOperationException), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnSecret, warning.Message, StringComparison.Ordinal);
        Assert.Equal("8", Assert.Single(rig.Registry.Loaded()).LastRejectedVersion);
    }

    [Fact]
    public async Task WhenTheDatabaseFailsOnAReload_ItPropagates_AndTheLastValidOneIsKept()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        var before = await rig.GetAsync(CompanyA);
        rig.Reader.Failure = new TimeoutException("base de datos caída");

        await Assert.ThrowsAsync<TimeoutException>(() => rig.Registry.ReloadAsync(CompanyA, CancellationToken.None));

        rig.Reader.Failure = null;
        Assert.Same(before, await rig.GetAsync(CompanyA));
        Assert.Equal("7", Assert.Single(rig.Registry.Loaded()).Version);
    }

    [Fact]
    public async Task ConcurrentReloadsOfOneCompany_AreSerialised_AndTheSecondFindsTheVersionAlreadyApplied()
    {
        var rig = NewRig();
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "7"), usesPlatform: false);
        await rig.GetAsync(CompanyA);
        rig.Reader.Set(CompanyA, Row(AssistantProvider.Ollama, "8"), usesPlatform: false);
        rig.Reader.Gate = new TaskCompletionSource();

        var first = rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        var second = rig.Registry.ReloadAsync(CompanyA, CancellationToken.None);
        rig.Reader.Gate.SetResult();
        await Task.WhenAll(first, second);

        // Sin serializar, la segunda lectura habría empezado antes de liberar la puerta (dos lecturas simultáneas).
        Assert.Equal(1, rig.Reader.MaxConcurrentReads);
        Assert.Equal(2, rig.Factory.Created.Count);
        Assert.Equal("8", Assert.Single(rig.Registry.Loaded()).Version);
    }
}
