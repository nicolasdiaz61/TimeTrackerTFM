using Anthropic;

using Microsoft.Extensions.AI;

using OllamaSharp;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Crea el cliente del proveedor elegido solo por configuración (<c>Ai:Provider</c>, DD-13). Sin proveedor devuelve
/// <c>null</c> y el asistente queda deshabilitado. El modelo y la clave salen siempre de <see cref="AiOptions"/>.
/// </summary>
public static class ChatClientFactory
{
    // El SDK reintenta por su cuenta ante errores transitorios; un solo reintento mantiene el peor caso dentro del tiempo máximo.
    private const int AnthropicMaxRetries = 1;

    public static IChatClient? Create(AiOptions options) => options.ResolvedProvider switch
    {
        AiProvider.Anthropic => CreateAnthropicClient(options).AsIChatClient(options.Model),
        AiProvider.Ollama => new OllamaApiClient(new Uri(options.Endpoint!), options.Model!),
        _ => null,
    };

    internal static AnthropicClient CreateAnthropicClient(AiOptions options)
        => new()
        {
            ApiKey = options.ApiKey,
            MaxRetries = AnthropicMaxRetries,
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };
}
