using System.Net;
using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantConnectionTesterTests
{
    private const string LeakedUrl = "https://api.ejemplo.test/v1";
    private const string LeakedKey = "clave-prueba-123456";

    private sealed class CountingClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing) => DisposeCount++;
    }

    private sealed class FixedFactory(IChatClient? client, Exception? failure = null) : IChatClientFactory
    {
        public List<AiOptions> Created { get; } = [];

        public IChatClient? Create(AiOptions options)
        {
            Created.Add(options);
            return failure is null ? client : throw failure;
        }
    }

    private sealed class Harness
    {
        public Harness(IChatClient? client = null, Exception? creationFailure = null, AiOptions? environment = null)
        {
            Client = new CountingClient(client ?? new FakeChatClient(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK"))));
            Factory = new FixedFactory(Client, creationFailure);
            Tester = new AssistantConnectionTester(Factory, Options.Create(environment ?? new AiOptions()), Clock, Logger);
        }

        public CountingClient Client { get; }

        public FixedFactory Factory { get; }

        public FakeTimeProvider Clock { get; } = new();

        public CapturingLogger<AssistantConnectionTester> Logger { get; } = new();

        public AssistantConnectionTester Tester { get; }

        public Task<ConnectionTestResult> RunAsync(AssistantConnectionCandidate? candidate = null, CancellationToken cancellationToken = default)
            => Tester.TestAsync(candidate ?? Candidate(), cancellationToken);
    }

    private static AssistantConnectionCandidate Candidate(
        AssistantProvider provider = AssistantProvider.OpenAI,
        string model = "modelo-ficticio",
        int timeoutSeconds = 25,
        AssistantEffort? effort = null,
        string? key = LeakedKey)
        => new(provider, model, timeoutSeconds, 800, effort, key is null ? null : new AssistantCredential(key));

    private static FakeChatClient Replying(string text = "OK") => new(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    private static FakeChatClient Failing(Exception exception) => new(_ => throw exception);

    [Theory]
    [InlineData("OK")]
    [InlineData("")]
    public async Task AnyCompletedResponse_IsOk(string text)
    {
        var harness = new Harness(Replying(text));

        var result = await harness.RunAsync();

        Assert.Equal(new ConnectionTestResult("ok", "Conexión correcta."), result);
    }

    [Fact]
    public async Task SendsOneFixedMessageWithoutToolsAnd64Tokens()
    {
        var fake = Replying();
        var harness = new Harness(fake);

        await harness.RunAsync(Candidate(effort: null));

        var (messages, options) = Assert.Single(fake.Calls);
        var message = Assert.Single(messages);
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Equal("Responde solo: OK.", message.Text);
        Assert.NotNull(options);
        Assert.Empty(options.Tools ?? []);
        Assert.Equal(64, options.MaxOutputTokens);
        Assert.Equal("modelo-ficticio", options.ModelId);
        Assert.Null(options.Reasoning);
    }

    [Theory]
    [InlineData(AssistantEffort.Low, ReasoningEffort.Low)]
    [InlineData(AssistantEffort.High, ReasoningEffort.High)]
    public async Task UsesTheSameChatOptionsAsTheChatWithTheCandidatesEffort(AssistantEffort effort, ReasoningEffort expected)
    {
        var fake = Replying();
        var harness = new Harness(fake);

        await harness.RunAsync(Candidate(effort: effort, model: "otro-modelo"));

        var options = Assert.Single(fake.Calls).Options;
        Assert.Equal(expected, options?.Reasoning?.Effort);
        Assert.Equal("otro-modelo", options?.ModelId);
    }

    [Fact]
    public async Task TheFactoryReceivesTheCandidateValuesAndTheEnvironmentEndpointOnly()
    {
        var environment = new AiOptions { Provider = "Anthropic", Model = "modelo-entorno", ApiKey = "clave-entorno", Endpoint = "http://localhost:11434/", MaxOutputTokens = 900 };
        var harness = new Harness(environment: environment);

        await harness.RunAsync(Candidate(AssistantProvider.Ollama, "modelo-candidato", timeoutSeconds: 40, effort: AssistantEffort.Medium, key: null));

        var created = Assert.Single(harness.Factory.Created);
        Assert.Equal(("Ollama", "modelo-candidato", 40, "Medium", null, 64), (created.Provider, created.Model, created.TimeoutSeconds, created.Effort, created.ApiKey, created.MaxOutputTokens));
        Assert.Equal("http://localhost:11434/", created.Endpoint);
        Assert.Equal("clave-entorno", environment.ApiKey);
    }

    [Fact]
    public async Task TheCandidateKeyIsUsedInsteadOfTheEnvironmentOne()
    {
        var harness = new Harness(environment: new AiOptions { ApiKey = "clave-entorno" });

        await harness.RunAsync(Candidate(key: "clave-candidata"));

        Assert.Equal("clave-candidata", Assert.Single(harness.Factory.Created).ApiKey);
    }

    [Theory]
    [InlineData(40, 15)]
    [InlineData(10, 10)]
    public async Task TheDeadlineIsTheShorterOfTheTimeoutAnd15Seconds(int timeoutSeconds, int expectedSeconds)
    {
        var hanging = new HangingChatClient();
        var harness = new Harness(hanging);
        var run = harness.RunAsync(Candidate(timeoutSeconds: timeoutSeconds));
        await hanging.Started;

        harness.Clock.Advance(TimeSpan.FromSeconds(expectedSeconds) - TimeSpan.FromMilliseconds(1));
        Assert.False(run.IsCompleted);
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var result = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("timeout", result.Result);
        Assert.Equal(1, harness.Client.DisposeCount);
    }

    [Fact]
    public async Task TheTemporaryClientIsDisposedOnSuccessAndOnFailure()
    {
        var success = new Harness();
        var failure = new Harness(Failing(new HttpRequestException("x", null, HttpStatusCode.Unauthorized)));

        await success.RunAsync();
        var failed = await failure.RunAsync();

        Assert.Equal(1, success.Client.DisposeCount);
        Assert.Equal(1, failure.Client.DisposeCount);
        Assert.Equal("authentication", failed.Result);
    }

    [Fact]
    public async Task ACreationFailure_IsUnreachableAndNeverLeaksItsMessage()
    {
        var harness = new Harness(creationFailure: new InvalidOperationException($"Ai:Endpoint {LeakedUrl} {LeakedKey}"));

        var result = await harness.RunAsync();

        Assert.Equal(ConnectionTestResults.Unreachable, result);
        Assert.DoesNotContain(harness.Logger.Entries, entry => entry.Message.Contains(LeakedUrl, StringComparison.Ordinal) || entry.Message.Contains(LeakedKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACallerCancellation_PropagatesAndStillDisposesTheClient()
    {
        var hanging = new HangingChatClient();
        var harness = new Harness(hanging);
        using var cancellation = new CancellationTokenSource();
        var run = harness.RunAsync(cancellationToken: cancellation.Token);
        await hanging.Started;

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(1, harness.Client.DisposeCount);
    }

    [Fact]
    public async Task TheLiveSnapshotAndItsClientAreNotTouched()
    {
        var live = Replying("respuesta del chat");
        using var chatClient = new AssistantChatClient(live, new AiOptions { Provider = "Ollama", Model = "modelo-vivo", Endpoint = "http://localhost:11434/" });
        var before = chatClient.Current;
        var harness = new Harness();

        await harness.RunAsync(Candidate(model: "otro-modelo"));

        Assert.Same(before, chatClient.Current);
        Assert.Equal("modelo-vivo", chatClient.Current.Options.Model);
        Assert.Empty(live.Calls);
    }

    public static TheoryData<Exception, string> LeakingFailures => new()
    {
        { new HttpRequestException($"401 en {LeakedUrl} con {LeakedKey}", null, HttpStatusCode.Unauthorized), "authentication" },
        { new InvalidOperationException($"fallo en {LeakedUrl} con {LeakedKey}"), "unreachable" },
        { new HttpRequestException($"404 {LeakedUrl} {LeakedKey}", null, HttpStatusCode.NotFound), "model-not-found" },
    };

    [Theory]
    [MemberData(nameof(LeakingFailures))]
    public async Task TheProviderFailureMessageReachesNeitherTheResponseNorTheLog(Exception failure, string category)
    {
        var harness = new Harness(Failing(failure));

        var result = await harness.RunAsync();

        var serialized = JsonSerializer.Serialize(result);
        Assert.Equal(category, result.Result);
        Assert.DoesNotContain(LeakedUrl, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(LeakedKey, serialized, StringComparison.Ordinal);
        var (level, message) = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains(failure.GetType().Name, message, StringComparison.Ordinal);
        Assert.Contains(category, message, StringComparison.Ordinal);
        Assert.DoesNotContain(LeakedUrl, message, StringComparison.Ordinal);
        Assert.DoesNotContain(LeakedKey, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASuccessfulTest_LogsOnlyTheProviderAndTheCategory()
    {
        var harness = new Harness();

        await harness.RunAsync(Candidate(model: "modelo-secreto"));

        var (level, message) = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Information, level);
        Assert.Contains("OpenAI", message, StringComparison.Ordinal);
        Assert.Contains("ok", message, StringComparison.Ordinal);
        Assert.DoesNotContain(LeakedKey, message, StringComparison.Ordinal);
        Assert.DoesNotContain("modelo-secreto", message, StringComparison.Ordinal);
    }
}
