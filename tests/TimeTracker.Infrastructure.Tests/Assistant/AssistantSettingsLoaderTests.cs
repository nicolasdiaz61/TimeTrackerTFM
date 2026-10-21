using System.Security.Cryptography;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Identity;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Persistence;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantSettingsLoaderTests : IDisposable
{
    private const string Secret = "clave-prueba-123456";

    private static readonly string KeyOne = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string KeyTwo = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private static AiOptions Environment(string? allowed = "Ollama,Anthropic,OpenAI")
        => new() { Provider = "Ollama", Model = "env-m", Endpoint = "http://localhost:11434/", AllowedProviders = allowed, TimeoutSeconds = 25 };

    private static AesGcmCredentialProtector Protector(string? active, params (string Version, string Key)[] keys)
    {
        var options = new AiOptions { ActiveMasterKeyVersion = active };
        foreach (var (version, key) in keys)
        {
            options.MasterKeys[version] = key;
        }

        return new AesGcmCredentialProtector(Options.Create(options));
    }

    private static AssistantSettings Row(
        AssistantProvider provider,
        string version = "5",
        string? model = "db-m",
        EncryptedCredential? credential = null,
        int? timeoutSeconds = null,
        AssistantEffort? effort = null)
        => new(provider, model, timeoutSeconds, null, effort, credential, Guid.NewGuid(), DateTimeOffset.UnixEpoch, version);

    private static EncryptedCredential Encrypt(AesGcmCredentialProtector protector, AssistantProvider provider)
        => protector.Protect(new AssistantCredential(Secret), provider);

    private Rig NewRig(AiOptions? environment = null, AesGcmCredentialProtector? protector = null, AssistantSettings? stored = null)
    {
        var rig = new Rig(environment ?? Environment(), protector ?? Protector("1", ("1", KeyOne)), stored);
        _disposables.Add(rig.Client);
        _disposables.Add(rig.Provider);
        return rig;
    }

    private sealed class Rig
    {
        public Rig(AiOptions environment, AesGcmCredentialProtector protector, AssistantSettings? stored)
        {
            Store.Stored = stored;
            Time = new FakeTimeProvider();
            InitialInner = new DisposalTrackingChatClient("entorno");
            Client = new AssistantChatClient(InitialInner, environment, Time);
            var services = new ServiceCollection();
            services.AddScoped<RecordingCompanyContext>();
            services.AddScoped<ICompanyContext>(scope => scope.GetRequiredService<RecordingCompanyContext>());
            services.AddScoped<ICompanyContextSetter>(scope => scope.GetRequiredService<RecordingCompanyContext>());
            // Registra la compañía que ve el almacén dentro del ámbito en el que lo resuelve el cargador.
            services.AddScoped<IAssistantSettingsStore>(scope =>
            {
                CompaniesSeenByTheStore.Add(scope.GetRequiredService<ICompanyContext>().CompanyId);
                return Store;
            });
            Provider = services.BuildServiceProvider();
            Scopes = new RecordingScopeFactory(new CompanyScopeFactory(Provider.GetRequiredService<IServiceScopeFactory>()));
            var options = Options.Create(environment);
            Loader = new AssistantSettingsLoader(
                Scopes, options, new ConfigurationAssistantEnvironment(options), protector, Factory, Client, Logger);
        }

        public FakeAssistantSettingsStore Store { get; } = new();

        public List<Guid?> CompaniesSeenByTheStore { get; } = [];

        public RecordingScopeFactory Scopes { get; }

        public RecordingChatClientFactory Factory { get; } = new();

        public CapturingLogger<AssistantSettingsLoader> Logger { get; } = new();

        public FakeTimeProvider Time { get; }

        public DisposalTrackingChatClient InitialInner { get; }

        public AssistantChatClient Client { get; }

        public ServiceProvider Provider { get; }

        public AssistantSettingsLoader Loader { get; }
    }

    [Fact]
    public async Task LoadAndReadVersion_AlwaysActInsideTheScopeOfTheDefaultCompany()
    {
        var rig = NewRig(stored: Row(AssistantProvider.Ollama, "9", model: "db-m"));

        await rig.Loader.ReadVersionAsync(CancellationToken.None);
        await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal([Company.DefaultId, Company.DefaultId], rig.Scopes.Requested);
        Assert.Equal([(Guid?)Company.DefaultId, Company.DefaultId], rig.CompaniesSeenByTheStore);
    }

    [Fact]
    public async Task LoadAsync_WithAValidRow_SwapsToTheEffectiveConfigurationUnderTheRowVersion()
    {
        var protector = Protector("1", ("1", KeyOne));
        var rig = NewRig(protector: protector, stored: Row(AssistantProvider.Anthropic, "17", "db-m", Encrypt(protector, AssistantProvider.Anthropic), timeoutSeconds: 40, effort: AssistantEffort.High));

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Applied, outcome);
        Assert.Equal("17", rig.Client.Current.Version);
        Assert.NotNull(rig.Client.Current.Pipeline);
        var created = Assert.Single(rig.Factory.Created);
        Assert.Equal(("Anthropic", "db-m", Secret, 40), (created.Provider, created.Model, created.ApiKey, created.TimeoutSeconds));
        Assert.Equal(AssistantEffort.High, created.ResolvedEffort);
        Assert.Equal("http://localhost:11434/", created.Endpoint);
        Assert.Null(rig.Client.Current.Options.ApiKey);
        Assert.DoesNotContain(rig.Logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task LoadAsync_WithARowThatOmitsValues_TakesThemFromTheEnvironment()
    {
        var rig = NewRig(stored: Row(AssistantProvider.Ollama, "3", model: null));

        await rig.Loader.LoadAsync(CancellationToken.None);

        var created = Assert.Single(rig.Factory.Created);
        Assert.Equal(("Ollama", "env-m", 25), (created.Provider, created.Model, created.TimeoutSeconds));
        Assert.Equal("3", rig.Client.Current.Version);
    }

    [Fact]
    public async Task LoadAsync_WithTheNoneProvider_DisablesTheAssistantUnderTheRowVersion()
    {
        var rig = NewRig(stored: Row(AssistantProvider.None, "9", model: null));

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Applied, outcome);
        Assert.False(rig.Client.IsEnabled);
        Assert.Equal("9", rig.Client.Current.Version);
    }

    [Fact]
    public async Task LoadAsync_WithoutARowAfterAStoredOne_RestoresTheEnvironmentUnderVersionZero()
    {
        var rig = NewRig(stored: Row(AssistantProvider.Ollama, "4"));
        await rig.Loader.LoadAsync(CancellationToken.None);
        rig.Store.Stored = null;

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Applied, outcome);
        Assert.Equal("0", rig.Client.Current.Version);
        Assert.Equal("env-m", rig.Client.Current.Options.Model);
    }

    [Fact]
    public async Task LoadAsync_WithoutARowOnTheInitialEnvironmentSnapshot_ChangesNothing()
    {
        var rig = NewRig();
        var before = rig.Client.Current;

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Unchanged, outcome);
        Assert.Same(before, rig.Client.Current);
        Assert.Empty(rig.Factory.Created);
    }

    [Fact]
    public async Task LoadAsync_WithTheVersionAlreadyApplied_DoesNotRebuildTheClient()
    {
        var rig = NewRig(stored: Row(AssistantProvider.Ollama, "4"));
        await rig.Loader.LoadAsync(CancellationToken.None);

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Unchanged, outcome);
        Assert.Single(rig.Factory.Created);
    }

    [Fact]
    public async Task LoadAsync_WithAnInvalidRow_KeepsTheLastValidOneWithoutCallingTheFactoryAndWarnsOncePerVersion()
    {
        var environment = Environment();
        environment.Model = null;
        var rig = NewRig(environment, stored: Row(AssistantProvider.Anthropic, "21", model: null));
        var before = rig.Client.Current;

        Assert.Equal(SettingsLoadOutcome.Rejected, await rig.Loader.LoadAsync(CancellationToken.None));
        Assert.Equal(SettingsLoadOutcome.Rejected, await rig.Loader.LoadAsync(CancellationToken.None));

        Assert.Same(before, rig.Client.Current);
        Assert.Empty(rig.Factory.Created);
        Assert.Equal("21", rig.Loader.LastRejectedVersion);
        var warning = Assert.Single(rig.Logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("invalid", warning.Message, StringComparison.Ordinal);
        Assert.Contains("21", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_WithAnotherRejectedVersion_WarnsAgain()
    {
        var environment = Environment();
        environment.Model = null;
        var rig = NewRig(environment, stored: Row(AssistantProvider.Anthropic, "21", model: null));
        await rig.Loader.LoadAsync(CancellationToken.None);
        rig.Store.Stored = Row(AssistantProvider.Anthropic, "22", model: null);

        await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(2, rig.Logger.Entries.Count);
        Assert.Contains("22", rig.Logger.Entries[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_WithATamperedCredential_IsRejectedAsUndecryptableAndNeverUsed()
    {
        var protector = Protector("1", ("1", KeyOne));
        var encrypted = Encrypt(protector, AssistantProvider.Anthropic);
        encrypted.Ciphertext[0] ^= 0x01;
        var rig = NewRig(protector: protector, stored: Row(AssistantProvider.Anthropic, "30", credential: encrypted));
        var before = rig.Client.Current;

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Rejected, outcome);
        Assert.Same(before, rig.Client.Current);
        Assert.Empty(rig.Factory.Created);
        var warning = Assert.Single(rig.Logger.Entries);
        Assert.Contains("undecryptable", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_WithARetiredKeyVersion_IsRejectedAsUndecryptable()
    {
        var writer = Protector("1", ("1", KeyOne));
        var encrypted = Encrypt(writer, AssistantProvider.Anthropic);
        var rig = NewRig(protector: Protector("2", ("2", KeyTwo)), stored: Row(AssistantProvider.Anthropic, "31", credential: encrypted));

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Rejected, outcome);
        Assert.Contains("undecryptable", Assert.Single(rig.Logger.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_WithoutAMasterKey_DoesNotUseTheStoredCredential()
    {
        var writer = Protector("1", ("1", KeyOne));
        var encrypted = Encrypt(writer, AssistantProvider.Anthropic);
        var rig = NewRig(protector: Protector(null), stored: Row(AssistantProvider.Anthropic, "32", credential: encrypted));

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Rejected, outcome);
        Assert.Empty(rig.Factory.Created);
        Assert.Equal("0", rig.Client.Current.Version);
    }

    [Fact]
    public async Task LoadAsync_WithAProviderThatIsNoLongerAllowed_IsRejectedAsNotAllowed()
    {
        var protector = Protector("1", ("1", KeyOne));
        var rig = NewRig(Environment(allowed: "Ollama,Anthropic"), protector, Row(AssistantProvider.Gemini, "40", credential: Encrypt(protector, AssistantProvider.Gemini)));

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Rejected, outcome);
        Assert.Empty(rig.Factory.Created);
        Assert.Contains("not-allowed", Assert.Single(rig.Logger.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_WhenTheFactoryFails_KeepsTheLastValidOneAndLogsOnlyTheExceptionType()
    {
        var rig = NewRig(stored: Row(AssistantProvider.Ollama, "50"));
        rig.Factory.Failure = new InvalidOperationException($"fallo con {Secret}");
        var before = rig.Client.Current;

        var outcome = await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsLoadOutcome.Rejected, outcome);
        Assert.Same(before, rig.Client.Current);
        var warning = Assert.Single(rig.Logger.Entries);
        Assert.Contains(nameof(InvalidOperationException), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_AfterARejectionASuccessfulLoadClearsTheRejectedVersion()
    {
        var environment = Environment();
        environment.Model = null;
        var rig = NewRig(environment, stored: Row(AssistantProvider.Anthropic, "60", model: null));
        await rig.Loader.LoadAsync(CancellationToken.None);
        rig.Store.Stored = Row(AssistantProvider.Ollama, "61", model: "db-m");

        await rig.Loader.LoadAsync(CancellationToken.None);

        Assert.Null(rig.Loader.LastRejectedVersion);
        Assert.Equal("61", rig.Client.Current.Version);
    }

    [Fact]
    public async Task LoadAsync_WhenTheDatabaseFails_PropagatesAndKeepsTheLastValidOne()
    {
        var rig = NewRig(stored: Row(AssistantProvider.Ollama, "70"));
        rig.Store.Failure = new TimeoutException("base de datos caída");
        var before = rig.Client.Current;

        await Assert.ThrowsAsync<TimeoutException>(() => rig.Loader.LoadAsync(CancellationToken.None));

        Assert.Same(before, rig.Client.Current);
    }

    [Fact]
    public async Task LoadAsync_SerialisesConcurrentLoads()
    {
        var rig = NewRig(stored: Row(AssistantProvider.Ollama, "80"));
        rig.Store.Gate = new TaskCompletionSource();

        var first = rig.Loader.LoadAsync(CancellationToken.None);
        var second = rig.Loader.LoadAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.Equal(1, rig.Store.GetCalls);
        rig.Store.Gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, rig.Store.MaxConcurrentReads);
        Assert.Equal([SettingsLoadOutcome.Applied, SettingsLoadOutcome.Unchanged], new[] { await first, await second });
        Assert.Single(rig.Factory.Created);
    }

    [Fact]
    public async Task ReadVersionAsync_ReturnsTheStoredVersionOrZero()
    {
        var rig = NewRig(stored: Row(AssistantProvider.Ollama, "90"));

        Assert.Equal("90", await rig.Loader.ReadVersionAsync(CancellationToken.None));
        rig.Store.Stored = null;
        Assert.Equal("0", await rig.Loader.ReadVersionAsync(CancellationToken.None));
    }
}
