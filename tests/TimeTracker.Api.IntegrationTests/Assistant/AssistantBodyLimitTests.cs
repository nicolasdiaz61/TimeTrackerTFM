using System.Net;

using TimeTracker.Infrastructure.Tests.Fakes;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>V-5 sobre Kestrel real (TestServer no aplica el límite de tamaño): el cuerpo de más de 32 KiB da 413 (no 500) con el pipeline real y sin invocar al modelo ni gastar cupo.</summary>
public class AssistantBodyLimitTests
{
    [Fact]
    public async Task OversizedBody_Returns413WithoutInvokingTheModelOrConsumingTheDailyQuota()
    {
        var model = new FakeChatClient(Reply("ok"));
        using var factory = new AssistantApiFactory(model, new Dictionary<string, string?> { ["Ai:DailyRequestsPerUser"] = "1" });
        factory.UseKestrel();
        var client = factory.CreateClient();

        var oversized = await client.AskAsync(new string('a', 40_000));
        var valid = await client.AskAsync("hola");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.Single(model.Calls);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task OversizedBody_ProblemJsonCarriesNoEchoOfTheText()
    {
        using var factory = new AssistantApiFactory(new FakeChatClient(Reply("ok")));
        factory.UseKestrel();

        var response = await factory.CreateClient().AskAsync("marca-secreta" + new string('a', 40_000));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain("marca-secreta", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OversizedBodyWithoutContentLength_AlsoReturns413()
    {
        var model = new FakeChatClient(Reply("ok"));
        using var factory = new AssistantApiFactory(model);
        factory.UseKestrel();
        var body = "{\"message\":\"" + new string('a', 40_000) + "\"}";
        using var content = new StreamContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)));
        content.Headers.ContentType = new("application/json");
        // Transferencia por fragmentos: sin Content-Length el servidor debe contar los bytes al leer.
        var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = content };
        request.Headers.TransferEncodingChunked = true;

        var response = await factory.CreateClient().SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(model.Calls);
    }
}
