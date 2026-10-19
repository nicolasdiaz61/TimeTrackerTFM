using TimeTracker.Application.Assistant.Configuration;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class TestAssistantConnectionUseCaseTests
{
    private static readonly AssistantSettingsInput Empty = new(null, null, null, null, null, null);

    private readonly InMemorySettingsStore _store = new();
    private readonly FakeProtector _protector = new();
    private readonly FakeTester _tester = new();
    private AssistantEnvironmentDefaults _env = Env(model: "env-m", timeoutSeconds: 30, maxOutputTokens: 1024);

    private Task<TestAssistantConnectionResult> Test(AssistantSettingsInput input)
        => new TestAssistantConnectionUseCase(_store, _protector, new FakeEnvironment(_env), _tester).ExecuteAsync(input, CancellationToken.None);

    [Fact]
    public async Task AnEmptyBody_TestsTheEffectiveConfiguration()
    {
        var result = await Test(Empty);

        Assert.Equal(new TestAssistantConnectionResult.Completed(new ConnectionTestResult("ok", "Conexión correcta.")), result);
        var candidate = Assert.Single(_tester.Received);
        Assert.Equal((AssistantProvider.Anthropic, "env-m", 30, 1024), (candidate.Provider, candidate.Model, candidate.TimeoutSeconds, candidate.MaxOutputTokens));
        Assert.Equal(SecretKey, candidate.Credential!.Value);
    }

    [Fact]
    public async Task OnlyTheModel_IsReplacedAndTheRestComesFromTheEffectiveValues()
    {
        _store.Row = Stored(model: "db-m", timeoutSeconds: 40);

        await Test(Empty with { Model = "otro" });

        var candidate = Assert.Single(_tester.Received);
        Assert.Equal(("otro", 40, 1024), (candidate.Model, candidate.TimeoutSeconds, candidate.MaxOutputTokens));
    }

    [Fact]
    public async Task TheEffectiveEffort_IsKeptWhenTheProviderDoesNotChange()
    {
        _env = Env(effort: AssistantEffort.Medium);

        await Test(Empty);

        Assert.Equal(AssistantEffort.Medium, _tester.Received.Single().Effort);
    }

    [Fact]
    public async Task AnInvalidCandidate_ReportsFieldErrorsWithoutCallingTheProvider()
    {
        var result = await Test(Empty with { TimeoutSeconds = 4 });

        var invalid = Assert.IsType<TestAssistantConnectionResult.Invalid>(result);
        Assert.Equal(["timeoutSeconds"], invalid.Errors.Select(e => e.Field));
        Assert.Empty(_tester.Received);
    }

    [Fact]
    public async Task ACandidateKey_IsUsedForTheTestButNeverStoredOrAudited()
    {
        _env = Env(apiKey: null);

        await Test(Empty with { Credential = SecretKey });

        Assert.Equal(SecretKey, _tester.Received.Single().Credential!.Value);
        Assert.Null(_store.Row);
        Assert.Equal(0, _store.Writes);
        Assert.Empty(_store.Audits);
    }

    [Fact]
    public async Task WithoutACandidateKey_TheStoredKeyIsDecryptedForTheSameProvider()
    {
        _env = Env(apiKey: null);
        _store.Row = Stored(credential: _protector.Protect(new AssistantCredential(SecretKey), AssistantProvider.Anthropic));

        await Test(Empty);

        Assert.Equal(SecretKey, _tester.Received.Single().Credential!.Value);
    }

    [Fact]
    public async Task AStoredKeyThatCannotBeDecrypted_IsRejectedOnTheCredentialFieldWithoutCallingTheTester()
    {
        _env = Env(apiKey: null);
        _store.Row = Stored(credential: _protector.Protect(new AssistantCredential(SecretKey), AssistantProvider.Anthropic));
        _protector.RetiredVersions.Add(1);

        var result = await Test(Empty);

        Assert.Equal(["credential"], Assert.IsType<TestAssistantConnectionResult.Invalid>(result).Errors.Select(e => e.Field));
        Assert.Empty(_tester.Received);
    }

    [Fact]
    public async Task WhenNothingIsStored_NothingChangesAfterTheTest()
    {
        _store.Row = Stored(model: "db-m");
        var before = _store.Row;

        await Test(Empty with { Model = "otro" });

        Assert.Same(before, _store.Row);
        Assert.Empty(_store.Audits);
    }

    [Fact]
    public async Task ADisabledAssistant_CannotBeTested()
    {
        _env = Env(provider: AssistantProvider.None);

        var result = await Test(Empty);

        Assert.Equal(["provider"], Assert.IsType<TestAssistantConnectionResult.Invalid>(result).Errors.Select(e => e.Field));
        Assert.Empty(_tester.Received);
    }

    [Fact]
    public async Task ExplicitlyTestingProviderNone_IsRejectedOnTheProviderFieldWithoutCallingTheTester()
    {
        var result = await Test(Empty with { Provider = "None" });

        Assert.Equal(["provider"], Assert.IsType<TestAssistantConnectionResult.Invalid>(result).Errors.Select(e => e.Field));
        Assert.Empty(_tester.Received);
    }

    [Fact]
    public async Task ACandidateKeyWithProviderNone_IsRejectedWithoutCallingTheTester()
    {
        var explicitNone = await Test(Empty with { Provider = "None", Credential = SecretKey });
        _env = Env(provider: AssistantProvider.None);
        var disabledEnvironment = await Test(Empty with { Credential = SecretKey });

        Assert.All([explicitNone, disabledEnvironment], r => Assert.IsType<TestAssistantConnectionResult.Invalid>(r));
        Assert.Empty(_tester.Received);
    }

    [Fact]
    public async Task TheProviderOutcome_IsReturnedAsIs()
    {
        var failing = new FakeTester(new ConnectionTestResult("authentication", "El proveedor rechazó la clave."));

        var result = await new TestAssistantConnectionUseCase(_store, _protector, new FakeEnvironment(_env), failing).ExecuteAsync(Empty, CancellationToken.None);

        Assert.Equal("authentication", Assert.IsType<TestAssistantConnectionResult.Completed>(result).Outcome.Result);
    }
}
