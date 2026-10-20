using TimeTracker.Web.Http;

namespace TimeTracker.Web.Assistant;

/// <summary>Fallo de una llamada de configuración: el error ya traducido, los mensajes por campo del 400 y el <c>Retry-After</c> del 429.</summary>
public sealed record AssistantConfigurationError(
    ApiRequestException Exception,
    IReadOnlyDictionary<string, string[]> FieldErrors,
    int? RetryAfterSeconds);
