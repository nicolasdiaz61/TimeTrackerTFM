using System.Net;
using System.Text;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Http;

namespace TimeTracker.Web.Tests.Support;

/// <summary>Sesión falsa: registra los tokens guardados sin tocar el navegador.</summary>
internal sealed class FakeSession : ISessionSignIn
{
    public List<string> Tokens { get; } = [];

    public Task SignInAsync(string token)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }
}

/// <summary>Manejador HTTP falso que guarda la petición y el cuerpo enviados.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
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

internal static class Responses
{
    public static HttpResponseMessage Json(HttpStatusCode status, string body, string mediaType = "application/json")
        => new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    public static HttpResponseMessage Problem(HttpStatusCode status, string detail)
        => Json(status, $$"""{"title":"Conflicto","detail":"{{detail}}"}""", "application/problem+json");

    public static HttpResponseMessage Validation(string field, string message)
        => Json(HttpStatusCode.BadRequest, $$$"""{"title":"Solicitud no válida","errors":{"{{{field}}}":["{{{message}}}"]}}""", "application/problem+json");

    public static HttpResponseMessage WithRetryAfter(HttpStatusCode status, int seconds)
    {
        var response = Json(status, """{"title":"Demasiadas solicitudes"}""", "application/problem+json");
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        return response;
    }
}

internal static class Failures
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFields = new Dictionary<string, string[]>();

    public static ApiFailure Of(HttpStatusCode? status, string message = "Mensaje de la API.", IReadOnlyDictionary<string, string[]>? fields = null, int? retryAfter = null)
        => new(new ApiRequestException(message, status), fields ?? NoFields, retryAfter);
}
