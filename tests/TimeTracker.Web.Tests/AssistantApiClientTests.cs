using System.Net;
using System.Text;
using System.Text.Json;

using TimeTracker.Web.Assistant;

namespace TimeTracker.Web.Tests;

public class AssistantApiClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static AssistantApiClient ClientFor(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    private static AssistantRequestDto Request() => new("Hola", [new AssistantTurnDto("user", "Antes")]);

    [Fact]
    public async Task AskAsync_PostsTheRequestToTheAssistantRoute_AndReturnsTheReply()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"reply":"Trabajó 8 horas."}""", Encoding.UTF8, "application/json") });

        var result = await ClientFor(handler).AskAsync(Request());

        Assert.Equal("Trabajó 8 horas.", result.Reply);
        Assert.Null(result.Error);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/asistente", handler.Request.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Hola", body.RootElement.GetProperty("message").GetString());
        Assert.Equal("user", body.RootElement.GetProperty("history")[0].GetProperty("role").GetString());
        Assert.Equal("Antes", body.RootElement.GetProperty("history")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task AskAsync_TooManyRequests_ReturnsTheDetailAndTheRetryAfterSeconds()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("""{"detail":"Demasiadas consultas."}""", Encoding.UTF8, "application/problem+json") };
            response.Headers.TryAddWithoutValidation("Retry-After", "30");
            return response;
        });

        var result = await ClientFor(handler).AskAsync(Request());

        Assert.Null(result.Reply);
        Assert.Equal("Demasiadas consultas.", result.Error!.Message);
        Assert.Equal(HttpStatusCode.TooManyRequests, result.Error.StatusCode);
        Assert.Equal(30, result.RetryAfterSeconds);
    }

    [Fact]
    public async Task AskAsync_RetryAfterAsHttpDate_IsIgnored()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", "Wed, 21 Oct 2026 07:28:00 GMT");
            return response;
        });

        var result = await ClientFor(handler).AskAsync(Request());

        Assert.Null(result.RetryAfterSeconds);
    }

    [Fact]
    public async Task AskAsync_NetworkFailure_ReturnsAGenericErrorWithoutThrowing()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("socket"));

        var result = await ClientFor(handler).AskAsync(Request());

        Assert.Null(result.Reply);
        Assert.Null(result.Error!.StatusCode);
    }
}
