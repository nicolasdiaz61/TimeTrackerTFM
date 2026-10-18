using Microsoft.Extensions.AI;

using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantChatPipelineTests
{
    private const string ToolName = "consulta_ficticia";
    private const string LeakedMessage = "detalle-interno-secreto";

    private static readonly List<ChatMessage> Question = [new(ChatRole.User, "¿Cuántas horas trabajé?")];

    private static ChatResponse CallTool(string name = ToolName, string callId = "c1")
        => new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(callId, name, new Dictionary<string, object?>())]));

    private static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    private static AIFunction Tool(Func<string> body) => AIFunctionFactory.Create(body, ToolName);

    private static Task<ChatResponse> RunAsync(FakeChatClient model, AiOptions options, params AITool[] tools)
        => AssistantChatPipeline.Build(model, options).GetResponseAsync(Question, AssistantChatPipeline.CreateChatOptions(options, tools));

    [Fact]
    public async Task CreateChatOptions_SendsTheConfiguredLimitsModelAndTools()
    {
        var model = new FakeChatClient(_ => Text("hecho"));
        var options = new AiOptions { Provider = "Ollama", Model = "modelo-ficticio", MaxOutputTokens = 300 };
        var tool = Tool(() => "ok");

        var response = await RunAsync(model, options, tool);

        Assert.Equal("hecho", response.Text);
        var sent = Assert.Single(model.Calls).Options!;
        Assert.Equal(300, sent.MaxOutputTokens);
        Assert.Equal("modelo-ficticio", sent.ModelId);
        Assert.Same(ChatToolMode.Auto, sent.ToolMode);
        Assert.Same(tool, Assert.Single(sent.Tools!));
    }

    [Fact]
    public async Task Pipeline_WithAModelThatAlwaysCallsATool_StopsAfterTheConfiguredIterations()
    {
        var model = new FakeChatClient(Enumerable.Repeat<Func<IReadOnlyList<ChatMessage>, ChatResponse>>(_ => CallTool(), 10).ToArray());
        var executions = 0;
        var options = new AiOptions { MaxToolIterations = 3 };

        var response = await RunAsync(model, options, Tool(() => { executions++; return "ok"; }));

        Assert.Equal(4, model.Calls.Count);
        Assert.Equal(3, executions);
        Assert.Equal(string.Empty, response.Text);
    }

    [Fact]
    public async Task Pipeline_WithConsecutiveToolFailures_CutsTheLoopAfterTwoTolerated()
    {
        var model = new FakeChatClient(Enumerable.Repeat<Func<IReadOnlyList<ChatMessage>, ChatResponse>>(_ => CallTool(), 10).ToArray());
        var options = new AiOptions { MaxToolIterations = 10 };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsync(model, options, Tool(() => throw new InvalidOperationException(LeakedMessage))));

        Assert.Equal(3, model.Calls.Count);
    }

    [Fact]
    public async Task Pipeline_WhenAToolFails_TheModelDoesNotReceiveTheExceptionTextOrType()
    {
        var model = new FakeChatClient(_ => CallTool(), _ => Text("lo siento"));

        await RunAsync(model, new AiOptions(), Tool(() => throw new InvalidOperationException(LeakedMessage)));

        var result = model.Calls[1].Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        var text = result.Result?.ToString() ?? string.Empty;
        Assert.NotEmpty(text);
        Assert.DoesNotContain(LeakedMessage, text);
        Assert.DoesNotContain(nameof(InvalidOperationException), text);
    }

    [Fact]
    public async Task Pipeline_WithSeveralCallsInOneTurn_RunsThemOneAtATime()
    {
        var model = new FakeChatClient(
            _ => new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("c1", ToolName, new Dictionary<string, object?>()),
                new FunctionCallContent("c2", ToolName, new Dictionary<string, object?>()),
                new FunctionCallContent("c3", ToolName, new Dictionary<string, object?>()),
            ])),
            _ => Text("listo"));
        int running = 0, maxRunning = 0, total = 0;
        var tool = AIFunctionFactory.Create(async () =>
        {
            maxRunning = Math.Max(maxRunning, Interlocked.Increment(ref running));
            await Task.Delay(20);
            Interlocked.Decrement(ref running);
            total++;
            return "ok";
        }, ToolName);

        await RunAsync(model, new AiOptions(), tool);

        Assert.Equal(3, total);
        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task Pipeline_WithAnUnknownFunction_DoesNotFailAndLetsTheModelRecover()
    {
        var model = new FakeChatClient(_ => CallTool("funcion_inexistente"), _ => Text("sin datos"));

        var response = await RunAsync(model, new AiOptions(), Tool(() => "ok"));

        Assert.Equal("sin datos", response.Text);
        Assert.Equal(2, model.Calls.Count);
    }
}
