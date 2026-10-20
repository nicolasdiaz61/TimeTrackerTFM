using System.ClientModel;
using System.Net;

using Anthropic.Exceptions;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Resultados fijos de la prueba de conexión (DD-13): categoría y texto en español, sin nada del proveedor.</summary>
internal static class ConnectionTestResults
{
    public static readonly ConnectionTestResult Ok = new("ok", "Conexión correcta.");
    public static readonly ConnectionTestResult Authentication = new("authentication", "El proveedor rechazó la clave.");
    public static readonly ConnectionTestResult ModelNotFound = new("model-not-found", "El proveedor no reconoce el modelo.");
    public static readonly ConnectionTestResult Rejected = new("rejected", "El proveedor rechazó los parámetros (modelo o esfuerzo no admitido).");
    public static readonly ConnectionTestResult Timeout = new("timeout", "El proveedor no respondió a tiempo.");
    public static readonly ConnectionTestResult Unreachable = new("unreachable", "No se pudo contactar con el proveedor.");
}

/// <summary>
/// Traduce el fallo de un proveedor a una categoría fija mirando el estado HTTP de la excepción de cada SDK (V-3), nunca su
/// mensaje. OpenAI: <c>ClientResultException.Status</c>; Anthropic: <c>AnthropicApiException.StatusCode</c>; Ollama:
/// <c>HttpRequestException.StatusCode</c>. Con un 400 Ollama lanza una excepción sin estado (<c>OllamaException</c> o
/// <c>InvalidOperationException</c>): no se puede distinguir de un fallo local, así que queda en <c>unreachable</c>.
/// </summary>
internal static class ConnectionFailureClassifier
{
    public static ConnectionTestResult Classify(Exception exception, bool deadlineExpired)
    {
        if (deadlineExpired || IsClientTimeout(exception))
        {
            return ConnectionTestResults.Timeout;
        }

        return HttpStatusOf(exception) switch
        {
            401 or 403 => ConnectionTestResults.Authentication,
            404 => ConnectionTestResults.ModelNotFound,
            400 or 422 => ConnectionTestResults.Rejected,
            _ => ConnectionTestResults.Unreachable,
        };
    }

    // El plazo propio de HttpClient (Ollama) y el de red de los SDK llegan como cancelación con TimeoutException dentro.
    private static bool IsClientTimeout(Exception exception)
        => Chain(exception).Any(current => current is TimeoutException);

    private static int? HttpStatusOf(Exception exception)
        => Chain(exception).Select(StatusOf).FirstOrDefault(status => status is not null);

    private static int? StatusOf(Exception exception) => exception switch
    {
        ClientResultException openAi => openAi.Status,
        AnthropicApiException anthropic => (int)anthropic.StatusCode,
        HttpRequestException { StatusCode: { } status } => (int)status,
        _ => null,
    };

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
