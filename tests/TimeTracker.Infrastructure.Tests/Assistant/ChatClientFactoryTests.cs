using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Anthropic;

using Microsoft.Extensions.AI;

using OllamaSharp;

using OpenAI;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

// Ninguna prueba abre una conexión: construir un cliente no hace red.
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class ChatClientFactoryTests
{
    private const string SecretKey = "clave-secreta-prueba";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("None")]
    public void Create_WithoutProvider_ReturnsNoClient(string? provider)
    {
        Assert.Null(ChatClientFactory.Create(new AiOptions { Provider = provider, Model = "modelo-ficticio", ApiKey = SecretKey }));
    }

    [Fact]
    public void Create_WithAnthropic_ReturnsTheAnthropicChatClientForTheConfiguredModel()
    {
        using var client = ChatClientFactory.Create(new AiOptions { Provider = "Anthropic", Model = "modelo-ficticio", ApiKey = SecretKey });

        Assert.NotNull(client);
        var metadata = client.GetService<ChatClientMetadata>();
        Assert.Equal("modelo-ficticio", metadata?.DefaultModelId);
        Assert.Contains("anthropic", metadata?.ProviderName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateAnthropicClient_UsesTheConfiguredKeyAndASingleRetry()
    {
        var client = ChatClientFactory.CreateAnthropicClient(new AiOptions { Provider = "Anthropic", Model = "m", ApiKey = SecretKey });

        Assert.IsType<AnthropicClient>(client);
        Assert.Equal(SecretKey, client.ApiKey);
        Assert.Equal(1, client.MaxRetries);
    }

    [Fact]
    public async Task AnthropicClient_SendsTheKeyToTheFixedUrlEvenIfTheEnvironmentSetsAnother()
    {
        const string variable = "ANTHROPIC_BASE_URL";
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "https://desvio.invalid/");
        try
        {
            var handler = new CountingHandler();
            var client = ChatClientFactory.CreateAnthropicClient(new AiOptions { Provider = "Anthropic", Model = "m", ApiKey = SecretKey });
            using var probe = new HttpClient(handler);
            var routed = client.WithOptions(options => options with { HttpClient = probe });

            await Assert.ThrowsAnyAsync<Exception>(() => routed.AsIChatClient("m").GetResponseAsync("hola"));

            Assert.Equal(ProviderEndpoints.Anthropic.Host, handler.LastHost);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public void CreateAnthropicClient_UsesTheConfiguredTimeout()
    {
        var client = ChatClientFactory.CreateAnthropicClient(new AiOptions { Provider = "Anthropic", Model = "m", ApiKey = SecretKey, TimeoutSeconds = 17 });

        Assert.Equal(TimeSpan.FromSeconds(17), client.Timeout);
    }

    [Fact]
    public void Create_WithOllama_ReturnsAnOllamaClientForTheConfiguredEndpointAndModel()
    {
        using var client = ChatClientFactory.Create(new AiOptions { Provider = "ollama", Model = "modelo-ficticio", Endpoint = "http://localhost:11434/" });

        var ollama = client?.GetService<OllamaApiClient>();
        Assert.NotNull(ollama);
        Assert.Equal("modelo-ficticio", ollama.SelectedModel);
        Assert.Equal(new Uri("http://localhost:11434/"), ollama.Uri);
    }

    [Fact]
    public void CreateOllamaClient_UsesTheEndpointAsBaseAddressAndTheConfiguredTimeout()
    {
        var handler = new CountingHandler();
        using var client = ChatClientFactory.CreateOllamaClient(
            new AiOptions { Provider = "Ollama", Model = "m", Endpoint = "http://localhost:11434/", TimeoutSeconds = 17 }, () => handler);

        var ollama = client.GetService<OllamaApiClient>();
        Assert.Equal(new Uri("http://localhost:11434/"), ollama?.Uri);
        Assert.Equal(TimeSpan.FromSeconds(17), ChatClientFactory.CreateOllamaHttpClient(
            new AiOptions { Endpoint = "http://localhost:11434/", TimeoutSeconds = 17 }, () => handler).Timeout);
    }

    [Fact]
    public async Task OllamaClient_GivesUpWhenTheServerDoesNotAnswerWithinTheTimeout()
    {
        using var client = ChatClientFactory.CreateOllamaClient(
            new AiOptions { Provider = "Ollama", Model = "m", Endpoint = "http://localhost:11434/", TimeoutSeconds = 1 }, () => new HangingHandler());
        var clock = System.Diagnostics.Stopwatch.StartNew();

        // WaitAsync es solo la red de seguridad de la prueba; la cota de tiempo distingue su TimeoutException de la del cliente.
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => client.GetResponseAsync("hola").WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(exception is TaskCanceledException or TimeoutException, $"Llegó {exception.GetType().Name}.");
        Assert.InRange(clock.Elapsed, TimeSpan.FromMilliseconds(800), TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task OllamaClient_DoesNotFollowARedirectToAnotherHost()
    {
        // Un 307 conserva el método y el cuerpo: seguirlo reenviaría la conversación a un host que el entorno no eligió.
        using var destination = new TcpListener(IPAddress.Parse("127.0.0.2"), 0);
        destination.Start();
        using var origin = new TcpListener(IPAddress.Loopback, 0);
        origin.Start();
        var destinationPort = ((IPEndPoint)destination.LocalEndpoint).Port;
        var originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
        var served = RedirectOnce(origin, $"http://127.0.0.2:{destinationPort}/api/chat");
        using var client = ChatClientFactory.CreateOllamaClient(
            new AiOptions { Provider = "Ollama", Model = "m", Endpoint = $"http://127.0.0.1:{originPort}/", TimeoutSeconds = 2 });

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetResponseAsync("hola").WaitAsync(TimeSpan.FromSeconds(10)));

        await served.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(destination.Pending(), "El destino de la redirección recibió una conexión.");
    }

    private static async Task RedirectOnce(TcpListener origin, string location)
    {
        using var connection = await origin.AcceptTcpClientAsync();
        var stream = connection.GetStream();
        var buffer = new byte[4096];
        var received = new StringBuilder();
        while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            received.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        var response = $"HTTP/1.1 307 Temporary Redirect\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
    }

    public static TheoryData<string> InvalidOllamaEndpoints => new()
    {
        "no-es-una-url-http",
        "/ruta/relativa",
        "ftp://localhost:11434/",
        "file:///etc/hosts",
    };

    [Theory]
    [MemberData(nameof(InvalidOllamaEndpoints))]
    public void CreateOllamaHttpClient_WithAnInvalidEndpoint_FailsBeforeCreatingAnyHandler(string endpoint)
    {
        var handlersCreated = 0;

        var exception = Assert.Throws<InvalidOperationException>(() => ChatClientFactory.CreateOllamaHttpClient(
            new AiOptions { Provider = "Ollama", Model = "m", Endpoint = endpoint },
            () =>
            {
                handlersCreated++;
                return new CountingHandler();
            }));

        Assert.Equal(0, handlersCreated);
        Assert.Contains("Ai:Endpoint", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(endpoint, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidOllamaEndpoints))]
    public void Create_WithOllamaAndAnInvalidEndpoint_FailsClearlyWithoutTheValue(string endpoint)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ChatClientFactory.Create(new AiOptions { Provider = "Ollama", Model = "m", Endpoint = endpoint }));

        Assert.Contains("Ai:Endpoint", exception.Message, StringComparison.Ordinal);
        Assert.Contains("http", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(endpoint, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateOllamaHttpClient_WithAValidEndpoint_CreatesTheHandlerOnce()
    {
        var handlersCreated = 0;

        using var http = ChatClientFactory.CreateOllamaHttpClient(
            new AiOptions { Provider = "Ollama", Model = "m", Endpoint = "https://ollama.ejemplo.test:11434/" },
            () =>
            {
                handlersCreated++;
                return new CountingHandler();
            });

        Assert.Equal(1, handlersCreated);
        Assert.Equal(new Uri("https://ollama.ejemplo.test:11434/"), http.BaseAddress);
    }

    [Fact]
    public void OllamaClient_ReleasesItsHttpClientWhenDisposed()
    {
        var handler = new CountingHandler();
        var client = ChatClientFactory.CreateOllamaClient(
            new AiOptions { Provider = "Ollama", Model = "m", Endpoint = "http://localhost:11434/" }, () => handler);

        client.Dispose();

        Assert.True(handler.Disposed);
    }

    public static TheoryData<string, string> MissingRequiredValues => new()
    {
        { "Anthropic", nameof(AiOptions.ApiKey) },
        { "Anthropic", nameof(AiOptions.Model) },
        { "OpenAI", nameof(AiOptions.ApiKey) },
        { "OpenAI", nameof(AiOptions.Model) },
        { "Gemini", nameof(AiOptions.ApiKey) },
        { "Gemini", nameof(AiOptions.Model) },
        { "Groq", nameof(AiOptions.ApiKey) },
        { "Groq", nameof(AiOptions.Model) },
        { "Ollama", nameof(AiOptions.Endpoint) },
        { "Ollama", nameof(AiOptions.Model) },
    };

    [Theory]
    [MemberData(nameof(MissingRequiredValues))]
    public void Create_WithoutARequiredValue_FailsClearlyAndWithoutSecrets(string provider, string missing)
    {
        var options = new AiOptions { Provider = provider, Model = "m", ApiKey = SecretKey, Endpoint = "http://localhost:11434/" };
        typeof(AiOptions).GetProperty(missing)!.SetValue(options, "  ");

        var exception = Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(options));

        Assert.Contains($"Ai:{missing}", exception.Message, StringComparison.Ordinal);
        Assert.Contains(provider, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WithAnUnrecognisedProvider_ReturnsNoClient()
    {
        Assert.Null(ChatClientFactory.Create(new AiOptions { Provider = "1", Model = "m", ApiKey = SecretKey }));
    }

    [Fact]
    public void Create_WithOpenAI_UsesTheSdkDefaultEndpointAndTheConfiguredModel()
    {
        using var client = ChatClientFactory.Create(new AiOptions { Provider = "OpenAI", Model = "modelo-ficticio", ApiKey = SecretKey });

        Assert.NotNull(client);
        var metadata = client.GetService<ChatClientMetadata>();
        Assert.Equal("modelo-ficticio", metadata?.DefaultModelId);
        Assert.Equal(new Uri("https://api.openai.com/v1"), metadata?.ProviderUri);
    }

    [Fact]
    public void Create_WithGemini_UsesTheFixedGeminiEndpoint()
    {
        using var client = ChatClientFactory.Create(new AiOptions { Provider = "Gemini", Model = "modelo-ficticio", ApiKey = SecretKey, Endpoint = "https://otra-url.invalid/" });

        Assert.NotNull(client);
        Assert.Equal(ProviderEndpoints.GeminiOpenAi, client.GetService<ChatClientMetadata>()?.ProviderUri);
    }

    [Fact]
    public void Create_WithGroq_UsesTheFixedGroqEndpoint()
    {
        using var client = ChatClientFactory.Create(new AiOptions { Provider = "Groq", Model = "modelo-ficticio", ApiKey = SecretKey, Endpoint = "https://otra-url.invalid/" });

        Assert.NotNull(client);
        Assert.Equal(ProviderEndpoints.GroqOpenAi, client.GetService<ChatClientMetadata>()?.ProviderUri);
        Assert.Equal(new Uri("https://api.groq.com/openai/v1"), ProviderEndpoints.GroqOpenAi);
    }

    [Fact]
    public void CreateOpenAIClientOptions_UsesTheConfiguredTimeoutAndNoEndpointForOpenAI()
    {
        var options = ChatClientFactory.CreateOpenAIClientOptions(new AiOptions { Provider = "OpenAI", TimeoutSeconds = 17 });

        Assert.Equal(TimeSpan.FromSeconds(17), options.NetworkTimeout);
        Assert.Null(options.Endpoint);
    }

    [Fact]
    public async Task OpenAIClient_RetriesOnceOnATransientFailure()
    {
        var handler = new CountingHandler();
        var options = ChatClientFactory.CreateOpenAIClientOptions(new AiOptions { Provider = "OpenAI" });
        options.Transport = new HttpClientPipelineTransport(new HttpClient(handler));
        using var client = new OpenAIClient(new ApiKeyCredential(SecretKey), options).GetChatClient("m").AsIChatClient();

        await Assert.ThrowsAsync<ClientResultException>(() => client.GetResponseAsync("hola"));

        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public void TheInjectableFactory_DelegatesToTheStaticOne()
    {
        IChatClientFactory factory = new DefaultChatClientFactory();

        Assert.Null(factory.Create(new AiOptions { Provider = "None" }));
        using var client = factory.Create(new AiOptions { Provider = "OpenAI", Model = "m", ApiKey = SecretKey });
        Assert.NotNull(client);
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage();
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public bool Disposed { get; private set; }

        public string? LastHost { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastHost = request.RequestUri?.Host;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) { Content = new StringContent("{}") });
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
