using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>
/// DD-14, «Tope y expulsión»: como mucho <c>Ai:MaxLoadedCompanies</c> compañías con proveedor cargado, expulsando la menos usada,
/// y caducidad por inactividad. Todo con <see cref="FakeTimeProvider"/>: ninguna espera real.
/// </summary>
public sealed class AssistantRuntimeEvictionTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private static Guid Company(int number) => Guid.Parse($"00000000-0000-4000-8000-{number:D12}");

    private static AssistantSettings Row(string version, string model)
        => new(AssistantProvider.Ollama, model, null, null, null, null, Guid.NewGuid(), DateTimeOffset.UnixEpoch, version);

    private Rig NewRig(int maxLoaded, int idleMinutes = 60)
    {
        var rig = new Rig(new AiOptions { Provider = null, AllowedProviders = "Ollama", Endpoint = "http://localhost:11434/", MaxLoadedCompanies = maxLoaded, RuntimeIdleMinutes = idleMinutes });
        _disposables.Add(rig.Registry);
        return rig;
    }

    private sealed class Rig
    {
        public Rig(AiOptions environment)
        {
            var options = Options.Create(environment);
            var protector = new AesGcmCredentialProtector(Options.Create(new AiOptions()));
            Registry = new AssistantRuntimeRegistry(
                Reader, new AssistantOptionsResolver(options, new ConfigurationAssistantEnvironment(options), protector), Factory, options, Time, Logger);
        }

        public FakeCompanySettingsReader Reader { get; } = new();

        public RecordingChatClientFactory Factory { get; } = new();

        public CapturingLogger<AssistantRuntimeRegistry> Logger { get; } = new();

        public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));

        public AssistantRuntimeRegistry Registry { get; }

        /// <summary>Compañía con proveedor propio (cuenta para el tope).</summary>
        public void WithProvider(int number) => Reader.Set(Company(number), Row("7", $"modelo-{number}"), usesPlatform: false);

        /// <summary>Compañía sin fila ni plataforma (asistente deshabilitado: no cuenta para el tope).</summary>
        public void Disabled(int number) => Reader.Set(Company(number), stored: null, usesPlatform: false);

        public async Task<AssistantSnapshot> AskAsync(int number)
        {
            var snapshot = await Registry.GetAsync(Company(number), CancellationToken.None);
            Time.Advance(TimeSpan.FromSeconds(1));
            return snapshot;
        }

        public IEnumerable<Guid> LoadedIds => Registry.Loaded().Select(info => info.CompanyId).Order();
    }

    [Fact]
    public async Task TheThirdCompanyWithAProvider_ExpelsTheLeastRecentlyUsed_WhenTheLimitIsTwo()
    {
        var rig = NewRig(maxLoaded: 2);
        foreach (var number in new[] { 1, 2, 3 })
        {
            rig.WithProvider(number);
        }

        await rig.AskAsync(1);
        await rig.AskAsync(2);
        await rig.AskAsync(3);

        Assert.Equal([Company(2), Company(3)], rig.LoadedIds);
    }

    [Fact]
    public async Task UsingACompanyAgain_ProtectsItFromTheNextExpulsion()
    {
        var rig = NewRig(maxLoaded: 2);
        foreach (var number in new[] { 1, 2, 3 })
        {
            rig.WithProvider(number);
        }

        await rig.AskAsync(1);
        await rig.AskAsync(2);
        await rig.AskAsync(1);
        await rig.AskAsync(3);

        Assert.Equal([Company(1), Company(3)], rig.LoadedIds);
    }

    [Fact]
    public async Task AnExpelledCompany_ReloadsItselfOnItsNextQuestion_AndNoQuestionIsRejectedForCapacity()
    {
        var rig = NewRig(maxLoaded: 1);
        rig.WithProvider(1);
        rig.WithProvider(2);
        await rig.AskAsync(1);
        await rig.AskAsync(2);
        Assert.Equal([Company(2)], rig.LoadedIds);

        var again = await rig.AskAsync(1);

        Assert.NotNull(again.Pipeline);
        Assert.Equal(2, rig.Reader.Reads[Company(1)]);
        Assert.Equal([Company(1)], rig.LoadedIds);
    }

    [Fact]
    public async Task AnInFlightRequestOfTheExpelledCompany_FinishesAndItsPipelineIsReleasedAfterTheGracePeriod()
    {
        var rig = NewRig(maxLoaded: 1);
        rig.WithProvider(1);
        rig.WithProvider(2);
        var inFlight = await rig.AskAsync(1);
        var expelledClient = rig.Factory.Clients[0];

        await rig.AskAsync(2);

        // La petición en curso conserva su instantánea y su pipeline sigue vivo durante la gracia.
        Assert.NotNull(inFlight.Pipeline);
        Assert.Equal(0, expelledClient.DisposeCount);
        // AskAsync ya avanzó 1 s tras la expulsión: faltan 118 s para cumplir 119 de los 120 de gracia.
        rig.Time.Advance(TimeSpan.FromSeconds(118));
        Assert.Equal(0, expelledClient.DisposeCount);
        rig.Time.Advance(TimeSpan.FromSeconds(2));
        Assert.True(expelledClient.DisposeCount > 0);
    }

    [Fact]
    public async Task DisabledCompanies_DoNotCountForTheLimit()
    {
        var rig = NewRig(maxLoaded: 1);
        rig.WithProvider(1);
        rig.Disabled(2);
        rig.Disabled(3);

        await rig.AskAsync(2);
        await rig.AskAsync(3);
        var enabled = await rig.AskAsync(1);

        Assert.NotNull(enabled.Pipeline);
        Assert.Equal([Company(1), Company(2), Company(3)], rig.LoadedIds);
    }

    [Fact]
    public async Task ASaveThatEnablesADisabledCompany_CountsForTheLimitFromThenOn()
    {
        var rig = NewRig(maxLoaded: 1);
        rig.WithProvider(1);
        rig.Disabled(2);
        await rig.AskAsync(1);
        await rig.AskAsync(2);
        rig.WithProvider(2);

        await rig.Registry.ReloadAsync(Company(2), CancellationToken.None);

        Assert.Equal([Company(2)], rig.LoadedIds);
    }

    [Fact]
    public async Task ConcurrentFirstQuestionsOfManyCompanies_LeaveExactlyTheLimitOfProvidersLoaded()
    {
        var rig = NewRig(maxLoaded: 3);
        foreach (var number in Enumerable.Range(1, 12))
        {
            rig.WithProvider(number);
        }

        // Todas las cargas quedan en vuelo a la vez y se liberan juntas: el tope se aplica con todas publicadas, no carga a carga.
        rig.Reader.Gate = new TaskCompletionSource();
        var calls = Enumerable.Range(1, 12).Select(number => Task.Run(() => rig.Registry.GetAsync(Company(number), CancellationToken.None).AsTask())).ToArray();
        await rig.Reader.WaitForReadsAsync(12);
        rig.Reader.Gate.SetResult();
        await Task.WhenAll(calls);

        Assert.Equal(3, rig.Registry.Loaded().Count);
        Assert.Equal(3, rig.Registry.EntryCount);
    }

    // --- Cola de expulsadas acotada ---

    [Fact]
    public async Task TheQueueOfExpelledPipelines_IsBoundedByTheLimit_ReleasingTheOldestEarly()
    {
        var rig = NewRig(maxLoaded: 2);
        foreach (var number in Enumerable.Range(1, 6))
        {
            rig.WithProvider(number);
        }

        foreach (var number in Enumerable.Range(1, 6))
        {
            await rig.AskAsync(number);
        }

        // Expulsadas por orden: 1, 2, 3 y 4. Con tope 2 solo quedan en gracia las dos últimas; las más antiguas se liberaron ya.
        Assert.Equal(2, rig.Registry.ExpelledCount);
        Assert.True(rig.Factory.Clients[0].DisposeCount > 0);
        Assert.True(rig.Factory.Clients[1].DisposeCount > 0);
        Assert.Equal(0, rig.Factory.Clients[2].DisposeCount);
        Assert.Equal(0, rig.Factory.Clients[3].DisposeCount);

        rig.Time.Advance(AssistantChatClient.ReleaseGracePeriod + TimeSpan.FromSeconds(1));

        Assert.Equal(0, rig.Registry.ExpelledCount);
        Assert.True(rig.Factory.Clients[3].DisposeCount > 0);
    }

    [Fact]
    public async Task AnExpelledPipelineWithinTheBound_KeepsItsFullGracePeriod()
    {
        var rig = NewRig(maxLoaded: 2);
        foreach (var number in new[] { 1, 2, 3 })
        {
            rig.WithProvider(number);
        }

        await rig.AskAsync(1);
        await rig.AskAsync(2);
        await rig.AskAsync(3);

        Assert.Equal(1, rig.Registry.ExpelledCount);
        Assert.Equal(0, rig.Factory.Clients[0].DisposeCount);
    }

    // --- Caducidad por inactividad ---

    [Fact]
    public async Task AnEntryIdleForTheConfiguredTime_IsExpiredAndReloadsOnItsNextQuestion()
    {
        var rig = NewRig(maxLoaded: 10, idleMinutes: 10);
        rig.WithProvider(1);
        rig.WithProvider(2);
        await rig.AskAsync(1);
        rig.Time.Advance(TimeSpan.FromMinutes(6));
        await rig.AskAsync(2);
        rig.Time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(1, rig.Registry.ExpireIdle());
        Assert.Equal([Company(2)], rig.LoadedIds);
        Assert.Equal(0, rig.Registry.ExpireIdle());

        await rig.AskAsync(1);
        Assert.Equal(2, rig.Reader.Reads[Company(1)]);
    }

    [Fact]
    public async Task IdleExpiryAlsoAppliesToDisabledEntries()
    {
        var rig = NewRig(maxLoaded: 10, idleMinutes: 5);
        rig.Disabled(1);
        await rig.AskAsync(1);
        rig.Time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(1, rig.Registry.ExpireIdle());

        Assert.Empty(rig.Registry.Loaded());
    }

    [Fact]
    public async Task AnEntryUsedJustBeforeTheLimit_IsNotExpired()
    {
        var rig = NewRig(maxLoaded: 10, idleMinutes: 10);
        rig.WithProvider(1);
        await rig.AskAsync(1);
        rig.Time.Advance(TimeSpan.FromMinutes(9));
        await rig.AskAsync(1);
        rig.Time.Advance(TimeSpan.FromMinutes(9));

        Assert.Equal(0, rig.Registry.ExpireIdle());
        Assert.Single(rig.Registry.Loaded());
    }

    // --- Aviso de sobrecarga con límite de frecuencia ---

    [Fact]
    public async Task TheOverloadWarning_IsRateLimited_AndRepeatsAfterTheQuietPeriod()
    {
        var rig = NewRig(maxLoaded: 1);
        foreach (var number in Enumerable.Range(1, 6))
        {
            rig.WithProvider(number);
        }

        await rig.AskAsync(1);
        await rig.AskAsync(2);
        await rig.AskAsync(3);
        await rig.AskAsync(4);
        Assert.Single(rig.Logger.Entries, entry => entry.Level == LogLevel.Warning);

        rig.Time.Advance(TimeSpan.FromMinutes(6));
        await rig.AskAsync(5);

        Assert.Equal(2, rig.Logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
        Assert.DoesNotContain(rig.Logger.Entries, entry => entry.Message.Contains(Company(1).ToString(), StringComparison.Ordinal) && entry.Message.Contains("clave", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DisposingTheRegistry_ReleasesTheLoadedPipelines()
    {
        var rig = NewRig(maxLoaded: 5);
        rig.WithProvider(1);
        rig.WithProvider(2);
        await rig.AskAsync(1);
        await rig.AskAsync(2);

        rig.Registry.Dispose();

        Assert.All(rig.Factory.Clients, client => Assert.True(client.DisposeCount > 0));
    }
}
