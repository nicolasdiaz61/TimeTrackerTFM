using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>
/// DD-15: el sondeo global lee las versiones de todas las compañías y recarga solo las ya cargadas que cambiaron. Reloj falso, registro
/// y almacén en memoria: la vuelta termina con una señal explícita, sin esperas por tiempo real.
/// </summary>
public sealed class AssistantSettingsPollerTests : IAsyncLifetime
{
    private const string Secret = "clave-prueba-123456";
    private const int PollSeconds = 7;

    private static readonly Guid CompanyA = Guid.Parse("00000000-0000-4000-8000-0000000000a1");
    private static readonly Guid CompanyB = Guid.Parse("00000000-0000-4000-8000-0000000000b1");
    private static readonly Guid CompanyC = Guid.Parse("00000000-0000-4000-8000-0000000000c1");

    private readonly FakeTimeProvider _time = new();
    private readonly FakeRuntimeRegistry _registry = new(new AssistantChatClient(null, new AiOptions()));
    private readonly FakeAssistantSettingsStore _store = new();
    private readonly CapturingLogger<AssistantSettingsPoller> _logger = new();
    private readonly ServiceProvider _provider;
    private readonly AssistantSettingsPoller _poller;

    public AssistantSettingsPollerTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAssistantSettingsStore>(_ => _store);
        _provider = services.BuildServiceProvider();
        _poller = new AssistantSettingsPoller(
            _registry, _provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new AiOptions { SettingsPollSeconds = PollSeconds }), _time, _logger);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _poller.StopAsync(CancellationToken.None);
        _poller.Dispose();
        await _provider.DisposeAsync();
    }

    private void Loaded(Guid company, string version, bool usesPlatform = false, string? rejected = null)
        => _registry.LoadedRuntimes.Add(new LoadedRuntime(company, version, usesPlatform, rejected));

    private void Stored(Guid company, string version, bool usesPlatform = false)
    {
        _store.Versions.RemoveAll(entry => entry.CompanyId == company);
        _store.Versions.Add(new CompanyVersion(company, version, usesPlatform));
    }

    // Avanza un intervalo y espera la señal explícita de «vuelta terminada» del sondeo (sin esperas por tiempo real).
    private async Task TickAsync()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted() => completed.TrySetResult();
        _poller.PollCompleted += OnCompleted;
        try
        {
            _time.Advance(TimeSpan.FromSeconds(PollSeconds));
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _poller.PollCompleted -= OnCompleted;
        }
    }

    [Fact]
    public async Task Poll_ReloadsOnlyTheLoadedCompaniesWhoseVersionChanged()
    {
        Loaded(CompanyA, "5");
        Loaded(CompanyB, "9");
        Stored(CompanyA, "5");
        Stored(CompanyB, "9");
        await _poller.StartAsync(CancellationToken.None);

        await TickAsync();
        Assert.Empty(_registry.Reloads);

        Stored(CompanyB, "10");
        await TickAsync();

        Assert.Equal([CompanyB], _registry.Reloads);
        Assert.Equal(2, _store.ListCalls);
    }

    [Fact]
    public async Task Poll_IgnoresCompaniesThatAreNotLoaded_EvenIfTheirVersionIsNew()
    {
        Loaded(CompanyA, "5");
        Stored(CompanyA, "5");
        Stored(CompanyC, "77");
        await _poller.StartAsync(CancellationToken.None);

        await TickAsync();

        Assert.Empty(_registry.Reloads);
    }

    [Fact]
    public async Task Poll_ANewRowForAnEntryWithVersionZero_CountsAsAChange_AndADeletedRowToo()
    {
        Loaded(CompanyA, "0");
        Loaded(CompanyB, "9");
        Stored(CompanyA, "31");
        Stored(CompanyB, "0");
        await _poller.StartAsync(CancellationToken.None);

        await TickAsync();

        Assert.Equal([CompanyA, CompanyB], _registry.Reloads.Order());
    }

    [Fact]
    public async Task Poll_AChangeOfThePlatformIndicator_ReloadsTheCompanyEvenWithTheSameVersion()
    {
        Loaded(CompanyA, "0", usesPlatform: true);
        Stored(CompanyA, "0", usesPlatform: false);
        await _poller.StartAsync(CancellationToken.None);

        await TickAsync();

        Assert.Equal([CompanyA], _registry.Reloads);
    }

    [Fact]
    public async Task Poll_DoesNotReadAgainAVersionThatWasAlreadyRejected()
    {
        Loaded(CompanyA, "5", rejected: "6");
        Stored(CompanyA, "6");
        await _poller.StartAsync(CancellationToken.None);

        await TickAsync();

        Assert.Empty(_registry.Reloads);
    }

    [Fact]
    public async Task Poll_LimitsTheNumberOfParallelReloads()
    {
        var all = Enumerable.Range(1, 12).Select(number => Guid.Parse($"00000000-0000-4000-8000-{number:D12}")).ToList();
        foreach (var company in all)
        {
            Loaded(company, "1");
            Stored(company, "2");
        }

        var release = new TaskCompletionSource();
        _registry.SignalAtInFlight = AssistantSettingsPoller.MaxParallelReloads;
        _registry.OnReload = (_, token) => release.Task.WaitAsync(token);
        await _poller.StartAsync(CancellationToken.None);

        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _poller.PollCompleted += () => completed.TrySetResult();
        _time.Advance(TimeSpan.FromSeconds(PollSeconds));
        await _registry.Reached.WaitAsync(TimeSpan.FromSeconds(10));

        // Con todas bloqueadas, el sondeo no ha lanzado más recargas que el máximo permitido.
        Assert.Equal(AssistantSettingsPoller.MaxParallelReloads, _registry.Reloads.Count);
        release.SetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(12, _registry.Reloads.Count);
        Assert.Equal(AssistantSettingsPoller.MaxParallelReloads, _registry.MaxConcurrentReloads);
    }

    [Fact]
    public async Task Poll_ExpiresIdleEntriesOnEveryLap()
    {
        await _poller.StartAsync(CancellationToken.None);

        await TickAsync();
        await TickAsync();

        Assert.Equal(2, _registry.Expirations);
    }

    [Fact]
    public async Task Poll_AFailingReloadDoesNotStopTheOthers_NorPropagates_AndWarnsOncePerStreak()
    {
        Loaded(CompanyA, "1");
        Loaded(CompanyB, "1");
        Stored(CompanyA, "2");
        Stored(CompanyB, "2");
        _registry.OnReload = (company, _) => company == CompanyA ? throw new TimeoutException($"fallo con {Secret}") : Task.CompletedTask;
        await _poller.StartAsync(CancellationToken.None);

        await TickAsync();
        await TickAsync();

        Assert.Equal(2, _registry.Reloads.Count(company => company == CompanyB));
        var warning = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(nameof(TimeoutException), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Poll_WhenTheDatabaseFails_WarnsOnce_AndLogsALineWhenItRecovers()
    {
        Loaded(CompanyA, "1");
        Stored(CompanyA, "1");
        await _poller.StartAsync(CancellationToken.None);
        _store.Failure = new TimeoutException("base de datos caída");

        await TickAsync();
        await TickAsync();
        Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Warning);

        _store.Failure = null;
        await TickAsync();

        Assert.Equal(LogLevel.Information, _logger.Entries[^1].Level);
        Assert.Equal(2, _logger.Entries.Count);
    }

    [Fact]
    public async Task Start_PreloadsOnlyThePlatformCompany_InsideTheBoundedFirstLoad()
    {
        await _poller.StartAsync(CancellationToken.None);

        Assert.Equal([Company.DefaultId], _registry.Gets);
    }

    [Fact]
    public async Task Start_WhenThePreloadTimesOut_StartsAnywayAndWarns()
    {
        var hanging = new HangingRegistry();
        var poller = new AssistantSettingsPoller(
            hanging, _provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new AiOptions { SettingsPollSeconds = PollSeconds }), _time, _logger);

        var starting = poller.StartAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(AssistantSettingsPoller.FirstLoadTimeoutSeconds + 1));
        await starting.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        await poller.StopAsync(CancellationToken.None);
        poller.Dispose();
    }

    [Fact]
    public async Task Start_WhenThePreloadFailsForInfrastructure_StartsAnywayAndWarnsOnlyTheExceptionType()
    {
        _registry.GetFailure = new TimeoutException($"fallo con {Secret}");

        await _poller.StartAsync(CancellationToken.None);

        var warning = Assert.Single(_logger.Entries);
        Assert.Contains(nameof(TimeoutException), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Poll_WhenAPollCompletedSubscriberThrows_KeepsPollingAndLogsOnlyTheExceptionType()
    {
        await _poller.StartAsync(CancellationToken.None);
        _poller.PollCompleted += () => throw new InvalidOperationException($"fallo del suscriptor con {Secret}");
        Loaded(CompanyA, "1");
        Stored(CompanyA, "2");

        await TickAsync();
        await TickAsync();

        Assert.Equal(2, _registry.Reloads.Count);
        Assert.All(_logger.Entries, entry => Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal));
        Assert.Contains(_logger.Entries, entry => entry.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
    }

    private sealed class HangingRegistry : IAssistantRuntimeRegistry
    {
        public async ValueTask<AssistantSnapshot> GetAsync(Guid companyId, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException();
        }

        public Task ReloadAsync(Guid companyId, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<LoadedRuntime> Loaded() => [];

        public int ExpireIdle() => 0;
    }
}
