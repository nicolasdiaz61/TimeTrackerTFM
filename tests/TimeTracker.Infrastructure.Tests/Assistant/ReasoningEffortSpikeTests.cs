using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json.Nodes;

using Anthropic;

using Microsoft.Extensions.AI;

using OllamaSharp;

using OpenAI;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

// Spike de DD-12 / V-2: comprueba, con el cuerpo real que serializa cada SDK, qué campo transporta el esfuerzo.
// Ninguna prueba abre una conexión: el manejador responde 400 y solo se inspecciona lo enviado.
public sealed class ReasoningEffortSpikeTests
{
    private const string FakeKey = "clave-prueba-no-real";
    private const string GeminiBase = "https://generativelanguage.googleapis.com/v1beta/openai/";

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest) { Content = new StringContent("{}") };
        }
    }

    private static async Task SendAsync(IChatClient client, string? effort)
    {
        var options = new AiOptions { Provider = "Ollama", Model = "modelo-ficticio", Effort = effort };
        try
        {
            await client.GetResponseAsync("hola", AssistantChatPipeline.CreateChatOptions(options, []));
        }
        catch (Exception exception) when (exception is ClientResultException or HttpRequestException or InvalidOperationException
            or Anthropic.Exceptions.AnthropicException or OllamaSharp.Models.Exceptions.OllamaException)
        {
            // Esperado: el manejador responde 400. Solo importa el cuerpo enviado.
        }
    }

    private static IChatClient OpenAiChat(RecordingHandler handler, Uri? endpoint)
    {
        var options = new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)), RetryPolicy = new ClientRetryPolicy(1) };
        if (endpoint is not null)
        {
            options.Endpoint = endpoint;
        }

        return new OpenAIClient(new ApiKeyCredential(FakeKey), options).GetChatClient("modelo-ficticio").AsIChatClient();
    }

    private static IChatClient AnthropicChat(RecordingHandler handler)
        => new AnthropicClient { ApiKey = FakeKey, MaxRetries = 0, HttpClient = new HttpClient(handler) }.AsIChatClient("modelo-ficticio");

    private static IChatClient OllamaChat(RecordingHandler handler)
        => new OllamaApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") }, "modelo-ficticio");

    private static JsonNode? Body(RecordingHandler handler) => JsonNode.Parse(Assert.IsType<string>(handler.Body));

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public async Task OpenAI_SendsReasoningEffortInTheBody(string effort)
    {
        var handler = new RecordingHandler();

        await SendAsync(OpenAiChat(handler, null), effort);

        Assert.Equal(effort, Body(handler)?["reasoning_effort"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public async Task Gemini_SendsReasoningEffortToTheFixedGeminiUrl(string effort)
    {
        var handler = new RecordingHandler();

        await SendAsync(OpenAiChat(handler, new Uri(GeminiBase)), effort);

        Assert.StartsWith(GeminiBase, handler.RequestUri?.ToString(), StringComparison.Ordinal);
        Assert.Equal(effort, Body(handler)?["reasoning_effort"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public async Task Anthropic_SendsOutputConfigEffort(string effort)
    {
        var handler = new RecordingHandler();

        await SendAsync(AnthropicChat(handler), effort);

        Assert.Equal(effort, Body(handler)?["output_config"]?["effort"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public async Task Ollama_SendsThink(string effort)
    {
        var handler = new RecordingHandler();

        await SendAsync(OllamaChat(handler), effort);

        Assert.Equal(effort, Body(handler)?["think"]?.GetValue<string>());
    }

    [Fact]
    public async Task WithoutEffort_NoProviderSendsAnyEffortField()
    {
        var openAi = new RecordingHandler();
        var gemini = new RecordingHandler();
        var anthropic = new RecordingHandler();
        var ollama = new RecordingHandler();

        await SendAsync(OpenAiChat(openAi, null), null);
        await SendAsync(OpenAiChat(gemini, new Uri(GeminiBase)), null);
        await SendAsync(AnthropicChat(anthropic), null);
        await SendAsync(OllamaChat(ollama), null);

        Assert.Null(Body(openAi)?["reasoning_effort"]);
        Assert.Null(Body(gemini)?["reasoning_effort"]);
        Assert.Null(Body(anthropic)?["output_config"]?["effort"]);
        Assert.Null(Body(anthropic)?["thinking"]);
        Assert.Null(Body(ollama)?["think"]);
    }
}
