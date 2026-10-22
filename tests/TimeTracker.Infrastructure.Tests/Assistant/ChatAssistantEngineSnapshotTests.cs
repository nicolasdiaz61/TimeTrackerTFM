using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>El motor toma la instantánea vigente una sola vez por petición (DD-10): un cambio a mitad no la afecta.</summary>
public sealed class ChatAssistantEngineSnapshotTests
{
    private const string MyTool = "get_my_time_entries";
    private static readonly AssistantConversation Question = new([], "¿Cuántas horas trabajé la semana pasada?");

    private static AiOptions Options(string model, int timeout = 25, int tokens = 800, string? effort = null)
        => new() { Provider = "Ollama", Model = model, Endpoint = "http://localhost:11434/", TimeoutSeconds = timeout, MaxOutputTokens = tokens, Effort = effort };

    private static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    private static ChatResponse CallTool() => new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", MyTool, new Dictionary<string, object?> { ["fromDate"] = "2026-10-12", ["toDate"] = "2026-10-18" })]));

    private sealed class Rig
    {
        public Rig(IChatClient? inner, AiOptions options)
        {
            Harness = new ToolHarness(ToolHarness.Luis, UserRole.Employee);
            Client = new AssistantChatClient(inner, options, Harness.Clock);
            Engine = new ChatAssistantEngine(Client, Harness.Toolset, new FixedCurrentUser(ToolHarness.Luis, UserRole.Employee), Harness.Clock, Logger);
        }

        public ToolHarness Harness { get; }

        public AssistantChatClient Client { get; }

        public CapturingLogger<ChatAssistantEngine> Logger { get; } = new();

        public ChatAssistantEngine Engine { get; }
    }

    [Fact]
    public async Task ReplyAsync_WhenTheSnapshotChangesMidRequest_FinishesWithTheInitialPipelineAndItsOptions()
    {
        var replacement = new FakeChatClient(_ => Text("desde la nueva"));
        Rig? rig = null;
        var initial = new FakeChatClient(
            _ =>
            {
                rig!.Client.Swap(replacement, Options("modelo-b", timeout: 40, tokens: 100, effort: "high"), "7");
                return CallTool();
            },
            _ => Text("desde la inicial"));
        rig = new Rig(initial, Options("modelo-a", timeout: 10, tokens: 300, effort: "low"));

        var reply = await rig.Engine.ReplyAsync(Question, CancellationToken.None);

        Assert.Equal("desde la inicial", reply);
        Assert.Equal(2, initial.Calls.Count);
        Assert.Empty(replacement.Calls);
        Assert.All(initial.Calls, call =>
        {
            Assert.Equal("modelo-a", call.Options!.ModelId);
            Assert.Equal(300, call.Options.MaxOutputTokens);
            Assert.Equal(ReasoningEffort.Low, call.Options.Reasoning?.Effort);
        });
        Assert.Contains("modelo-a", Assert.Single(rig.Logger.Entries).Message);
    }

    [Fact]
    public async Task ReplyAsync_WhenTheSnapshotChangesMidRequest_KeepsTheInitialTimeout()
    {
        var hanging = new HangingChatClient();
        var rig = new Rig(hanging, Options("modelo-a", timeout: 10));

        var pending = rig.Engine.ReplyAsync(Question, CancellationToken.None);
        await hanging.Started;
        rig.Client.Swap(new FakeChatClient(), Options("modelo-b", timeout: 40), "7");
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(11));

        Assert.Null(await pending);
        Assert.Contains("timeout", Assert.Single(rig.Logger.Entries).Message);
    }

    [Fact]
    public async Task ReplyAsync_TheNextRequestUsesTheNewSnapshot()
    {
        var initial = new FakeChatClient(_ => Text("a"));
        var replacement = new FakeChatClient(_ => Text("b"));
        var rig = new Rig(initial, Options("modelo-a"));

        Assert.Equal("a", await rig.Engine.ReplyAsync(Question, CancellationToken.None));
        rig.Client.Swap(replacement, Options("modelo-b", tokens: 128), "8");

        Assert.Equal("b", await rig.Engine.ReplyAsync(Question, CancellationToken.None));
        Assert.Equal(("modelo-b", 128), (replacement.Calls[0].Options!.ModelId, replacement.Calls[0].Options!.MaxOutputTokens));
    }

    [Fact]
    public async Task ReplyAsync_WhenTheAssistantIsDisabledMidRequest_StillAnswersThatRequestAndTheNextOneGetsNull()
    {
        Rig? rig = null;
        var initial = new FakeChatClient(_ =>
        {
            rig!.Client.Swap(null, new AiOptions(), "9");
            return Text("respuesta en curso");
        });
        rig = new Rig(initial, Options("modelo-a"));

        Assert.Equal("respuesta en curso", await rig.Engine.ReplyAsync(Question, CancellationToken.None));

        Assert.False(rig.Engine.IsEnabled);
        Assert.Null(await rig.Engine.ReplyAsync(Question, CancellationToken.None));
    }

    [Fact]
    public void TheDailyQuota_KeepsFollowingTheEnvironmentNotTheSnapshot()
    {
        var environment = Microsoft.Extensions.Options.Options.Create(new AiOptions { DailyRequestsPerUser = 1 });
        var quota = new InMemoryAssistantQuota(environment, new Microsoft.Extensions.Time.Testing.FakeTimeProvider());
        var rig = new Rig(new FakeChatClient(), Options("modelo-a"));
        var generous = Options("modelo-b");
        generous.DailyRequestsPerUser = 100;
        generous.RequestsPerMinute = 100;

        rig.Client.Swap(new FakeChatClient(), generous, "10");

        Assert.True(quota.TryConsumeDaily(Guid.Empty, ToolHarness.Luis).Allowed);
        Assert.False(quota.TryConsumeDaily(Guid.Empty, ToolHarness.Luis).Allowed);
    }
}
