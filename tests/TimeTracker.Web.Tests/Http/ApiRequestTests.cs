using System.Net;
using System.Text;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Tests.Http;

public class ApiRequestTests
{
    private static readonly HashSet<HttpStatusCode> AssistantStatuses = [HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable];

    private static HttpResponseMessage Problem(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/problem+json") };

    private sealed record Payload(string? Value);

    [Fact]
    public async Task SendAsync_Conflict_ReturnsDetailWithTheExistingOverload()
    {
        var exception = await Assert.ThrowsAsync<ApiRequestException>(() => ApiRequest.SendAsync<Payload>(
            () => Task.FromResult(Problem(HttpStatusCode.Conflict, """{"detail":"Ya hay un fichaje abierto."}""")), CancellationToken.None));

        Assert.Equal("Ya hay un fichaje abierto.", exception.Message);
        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendAsync_ExistingOverload_DoesNotReproduceTheDetailOfOtherStatuses(HttpStatusCode status)
    {
        var exception = await Assert.ThrowsAsync<ApiRequestException>(() => ApiRequest.SendAsync<Payload>(
            () => Task.FromResult(Problem(status, """{"detail":"Texto del servidor."}""")), CancellationToken.None));

        Assert.Equal(ApiRequest.GenericErrorMessage, exception.Message);
        Assert.Equal(status, exception.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendAsync_WithDetailStatuses_ReturnsTheDetail(HttpStatusCode status)
    {
        var exception = await Assert.ThrowsAsync<ApiRequestException>(() => ApiRequest.SendAsync<Payload>(
            () => Task.FromResult(Problem(status, """{"detail":"Texto en español de la API."}""")), AssistantStatuses, CancellationToken.None));

        Assert.Equal("Texto en español de la API.", exception.Message);
        Assert.Equal(status, exception.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WithDetailStatuses_UsesTheGenericMessageWhenThereIsNoUsableDetail()
    {
        var exception = await Assert.ThrowsAsync<ApiRequestException>(() => ApiRequest.SendAsync<Payload>(
            () => Task.FromResult(Problem(HttpStatusCode.ServiceUnavailable, "no es json")), AssistantStatuses, CancellationToken.None));

        Assert.Equal(ApiRequest.GenericErrorMessage, exception.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task SendAsync_WithDetailStatuses_DoesNotReproduceTheBodyOfOtherStatuses(HttpStatusCode status)
    {
        var exception = await Assert.ThrowsAsync<ApiRequestException>(() => ApiRequest.SendAsync<Payload>(
            () => Task.FromResult(Problem(status, """{"detail":"Traza interna secreta."}""")), AssistantStatuses, CancellationToken.None));

        Assert.Equal(ApiRequest.GenericErrorMessage, exception.Message);
        Assert.DoesNotContain("secreta", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_KeepsTheStatusForTheCommonSessionFlow()
    {
        var exception = await Assert.ThrowsAsync<ApiRequestException>(() => ApiRequest.SendAsync<Payload>(
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)), AssistantStatuses, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(ApiRequest.GenericErrorMessage, exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithDetailStatuses_ReturnsTheBodyOnSuccess()
    {
        var result = await ApiRequest.SendAsync<Payload>(
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"value":"hola"}""", Encoding.UTF8, "application/json") }),
            AssistantStatuses,
            CancellationToken.None);

        Assert.Equal("hola", result.Value);
    }

    [Fact]
    public async Task SendAsync_WithDetailStatuses_NetworkFailureIsGeneric()
    {
        var exception = await Assert.ThrowsAsync<ApiRequestException>(() => ApiRequest.SendAsync<Payload>(
            () => throw new HttpRequestException("socket"), AssistantStatuses, CancellationToken.None));

        Assert.Null(exception.StatusCode);
        Assert.Equal(ApiRequest.GenericErrorMessage, exception.Message);
    }
}
