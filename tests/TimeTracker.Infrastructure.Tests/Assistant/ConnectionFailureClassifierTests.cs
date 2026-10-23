using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;

using Anthropic;

using Microsoft.Extensions.AI;

using OllamaSharp;

using OpenAI;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

// Las excepciones se obtienen de los SDK reales contra un manejador que solo devuelve un estado (sin red): así se prueban los
// tipos y la propiedad de estado confirmados en V-3, no excepciones fabricadas a mano. El cuerpo de error engaña a propósito
// (habla de otro estado) para comprobar que se clasifica por el estado HTTP y no por el texto.
public sealed class ConnectionFailureClassifierTests
{
    private const string MisleadingBody = """{"error":{"message":"401 invalid api key, 404 model not found, 400 bad request"}}""";

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(MisleadingBody) });
    }

    private static async Task<Exception> FailureAsync(string provider, HttpStatusCode status)
    {
        var http = new HttpClient(new StatusHandler(status)) { BaseAddress = new Uri("http://localhost:11434/") };
        IChatClient client = provider switch
        {
            "Anthropic" => new AnthropicClient { ApiKey = "clave-prueba-no-real", MaxRetries = 0, HttpClient = http }.AsIChatClient("modelo-ficticio"),
            "Ollama" => new OllamaApiClient(http, "modelo-ficticio"),
            _ => new OpenAIClient(
                new ApiKeyCredential("clave-prueba-no-real"),
                new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(http), RetryPolicy = new ClientRetryPolicy(0) })
                .GetChatClient("modelo-ficticio").AsIChatClient(),
        };

        using (client)
        {
            return await Assert.ThrowsAnyAsync<Exception>(() => client.GetResponseAsync("hola"));
        }
    }

    public static TheoryData<string, HttpStatusCode, string> StatusCases
    {
        get
        {
            var cases = new TheoryData<string, HttpStatusCode, string>();
            foreach (var provider in new[] { "OpenAI", "Gemini", "Groq", "Anthropic", "Ollama" })
            {
                cases.Add(provider, HttpStatusCode.Unauthorized, "authentication");
                cases.Add(provider, HttpStatusCode.Forbidden, "authentication");
                cases.Add(provider, HttpStatusCode.NotFound, "model-not-found");
                cases.Add(provider, HttpStatusCode.UnprocessableEntity, "rejected");
                cases.Add(provider, HttpStatusCode.TooManyRequests, "unreachable");
                cases.Add(provider, HttpStatusCode.InternalServerError, "unreachable");
                cases.Add(provider, HttpStatusCode.BadGateway, "unreachable");
            }

            // Ollama responde 400 con una excepción sin estado (V-3), así que no se puede clasificar como rechazo (ver más abajo).
            cases.Add("OpenAI", HttpStatusCode.BadRequest, "rejected");
            cases.Add("Anthropic", HttpStatusCode.BadRequest, "rejected");
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(StatusCases))]
    public async Task Failure_IsClassifiedByTheHttpStatusOfEachSdk(string provider, HttpStatusCode status, string expected)
    {
        var failure = await FailureAsync(provider, status);

        Assert.Equal(expected, ConnectionFailureClassifier.Classify(failure, deadlineExpired: false).Result);
    }

    [Fact]
    public async Task OllamaBadRequest_HasNoStatusAndStaysUnreachable()
    {
        var failure = await FailureAsync("Ollama", HttpStatusCode.BadRequest);

        // Decisión: sin estado HTTP no hay prueba de que sea un rechazo de parámetros (InvalidOperationException es genérica).
        Assert.False(failure is HttpRequestException);
        Assert.Equal("unreachable", ConnectionFailureClassifier.Classify(failure, deadlineExpired: false).Result);
    }

    [Fact]
    public void StatusInAnInnerException_IsFoundThroughTheChain()
    {
        var wrapped = new InvalidOperationException("envoltorio", new HttpRequestException("x", inner: null, HttpStatusCode.Unauthorized));

        Assert.Equal("authentication", ConnectionFailureClassifier.Classify(wrapped, deadlineExpired: false).Result);
    }

    [Theory]
    [InlineData("401 unauthorized: invalid api key")]
    [InlineData("404 model not found")]
    [InlineData("400 bad request, timeout, unauthorized")]
    public void TheMessageNeverDecidesTheCategory(string message)
    {
        Assert.Equal("unreachable", ConnectionFailureClassifier.Classify(new InvalidOperationException(message), deadlineExpired: false).Result);
        Assert.Equal("unreachable", ConnectionFailureClassifier.Classify(new HttpRequestException(message), deadlineExpired: false).Result);
    }

    [Fact]
    public void ExpiredDeadline_IsTimeoutWhateverTheException()
    {
        Assert.Equal("timeout", ConnectionFailureClassifier.Classify(new OperationCanceledException(), deadlineExpired: true).Result);
        Assert.Equal("timeout", ConnectionFailureClassifier.Classify(new InvalidOperationException("x"), deadlineExpired: true).Result);
    }

    [Fact]
    public void ClientSideTimeout_IsTimeoutEvenWithoutTheOwnDeadline()
    {
        var clientTimeout = new TaskCanceledException("x", new TimeoutException());

        Assert.Equal("timeout", ConnectionFailureClassifier.Classify(clientTimeout, deadlineExpired: false).Result);
    }

    [Theory]
    [InlineData("ok", "Conexión correcta.")]
    [InlineData("authentication", "El proveedor rechazó la clave.")]
    [InlineData("model-not-found", "El proveedor no reconoce el modelo.")]
    [InlineData("rejected", "El proveedor rechazó los parámetros (modelo o esfuerzo no admitido).")]
    [InlineData("timeout", "El proveedor no respondió a tiempo.")]
    [InlineData("unreachable", "No se pudo contactar con el proveedor.")]
    public void EveryCategory_HasItsFixedSpanishMessage(string category, string message)
    {
        var all = new[]
        {
            ConnectionTestResults.Ok, ConnectionTestResults.Authentication, ConnectionTestResults.ModelNotFound,
            ConnectionTestResults.Rejected, ConnectionTestResults.Timeout, ConnectionTestResults.Unreachable,
        };

        Assert.Equal(new ConnectionTestResult(category, message), Assert.Single(all, result => result.Result == category));
    }
}
