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

    // Tabla proveedor → ruta del campo donde cada SDK serializa el esfuerzo.
    private static readonly Dictionary<string, string[]> EffortFields = new()
    {
        ["OpenAI"] = ["reasoning_effort"],
        ["Gemini"] = ["reasoning_effort"],
        ["Groq"] = ["reasoning_effort"],
        ["Anthropic"] = ["output_config", "effort"],
        ["Ollama"] = ["think"],
    };

    public static TheoryData<string, string> EffortCases
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (var provider in EffortFields.Keys)
            {
                foreach (var effort in new[] { "low", "medium", "high" })
                {
                    cases.Add(provider, effort);
                }
            }

            return cases;
        }
    }

    public static TheoryData<string> Providers => [.. EffortFields.Keys];

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

    private static async Task<JsonNode?> SendAsync(string provider, string? effort, RecordingHandler handler)
    {
        var options = new AiOptions { Provider = provider, Model = "modelo-ficticio", Effort = effort };
        using var client = BuildClient(options, handler);
        try
        {
            await client.GetResponseAsync("hola", AssistantChatPipeline.CreateChatOptions(options, []));
        }
        catch (Exception exception) when (exception is ClientResultException or HttpRequestException or InvalidOperationException
            or Anthropic.Exceptions.AnthropicException or OllamaSharp.Models.Exceptions.OllamaException)
        {
            // Esperado: el manejador responde 400. Solo importa el cuerpo enviado.
        }

        return JsonNode.Parse(Assert.IsType<string>(handler.Body));
    }

    private static IChatClient BuildClient(AiOptions options, RecordingHandler handler)
    {
        var http = new HttpClient(handler);
        switch (options.ResolvedProvider)
        {
            case AiProvider.Anthropic:
                return new AnthropicClient { ApiKey = FakeKey, MaxRetries = 0, HttpClient = http }.AsIChatClient(options.Model);
            case AiProvider.Ollama:
                return new OllamaApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") }, options.Model!);
            default:
                // Las opciones reales de la fábrica: así la URL de Gemini y el resto salen del código de producción.
                var clientOptions = ChatClientFactory.CreateOpenAIClientOptions(options);
                clientOptions.Transport = new HttpClientPipelineTransport(http);
                return new OpenAIClient(new ApiKeyCredential(FakeKey), clientOptions).GetChatClient(options.Model).AsIChatClient();
        }
    }

    private static JsonNode? Field(JsonNode? body, string[] path)
        => path.Aggregate(body, (node, name) => node?[name]);

    [Theory]
    [MemberData(nameof(EffortCases))]
    public async Task EachProvider_SendsTheEffortInItsOwnField(string provider, string effort)
    {
        var handler = new RecordingHandler();

        var body = await SendAsync(provider, effort, handler);

        Assert.Equal(effort, Field(body, EffortFields[provider])?.GetValue<string>());
    }

    [Fact]
    public async Task Gemini_SendsTheRequestToTheFixedGeminiUrl()
    {
        var handler = new RecordingHandler();

        await SendAsync("Gemini", "low", handler);

        Assert.StartsWith(ProviderEndpoints.GeminiOpenAi.ToString(), handler.RequestUri?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Groq_SendsTheRequestToTheFixedGroqUrl()
    {
        var handler = new RecordingHandler();

        await SendAsync("Groq", "low", handler);

        Assert.StartsWith(ProviderEndpoints.GroqOpenAi.ToString(), handler.RequestUri?.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task WithoutEffort_NoProviderSendsAnyEffortField(string provider)
    {
        var body = await SendAsync(provider, null, new RecordingHandler());

        Assert.Null(Field(body, EffortFields[provider]));
        if (provider == "Anthropic")
        {
            Assert.Null(body?["thinking"]);
        }
    }
}
