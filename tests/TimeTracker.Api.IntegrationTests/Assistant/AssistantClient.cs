using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.AI;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>Utilidades compartidas por las pruebas del endpoint del asistente.</summary>
internal static class AssistantClient
{
    public const string Route = "/api/asistente";
    public const string DisabledDetail = "El asistente no está disponible en este entorno.";
    public const string FailedDetail = "El asistente no está disponible en este momento. Inténtelo de nuevo más tarde.";

    public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    public static Func<IReadOnlyList<ChatMessage>, ChatResponse> Reply(string text) => _ => Text(text);

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string json)
        => client.PostAsync(Route, new StringContent(json, Encoding.UTF8, "application/json"), CancellationToken.None);

    public static Task<HttpResponseMessage> AskAsync(this HttpClient client, string message, params (string? Role, string Text)[] history)
    {
        var body = new JsonObject { ["message"] = message };
        if (history.Length > 0)
        {
            body["history"] = new JsonArray(history.Select(turn => (JsonNode)new JsonObject { ["role"] = turn.Role, ["text"] = turn.Text }).ToArray());
        }

        return client.PostJsonAsync(body.ToJsonString());
    }

    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));

    public static async Task<string> ProblemDetailAsync(HttpResponseMessage response, int status)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ReadAsync(response);
        Assert.Equal(status, body.RootElement.GetProperty("status").GetInt32());
        return body.RootElement.GetProperty("detail").GetString()!;
    }
}
