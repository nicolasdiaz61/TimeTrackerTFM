using System.ClientModel;
using System.ClientModel.Primitives;

using Anthropic;

using Microsoft.Extensions.AI;

using OllamaSharp;

using OpenAI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Crea el cliente del proveedor elegido solo por configuración (<c>Ai:Provider</c>, DD-3). Sin proveedor devuelve
/// <c>null</c> y el asistente queda deshabilitado. El modelo y la clave salen siempre de <see cref="AiOptions"/>; la URL
/// de cada proveedor es fija (<see cref="ProviderEndpoints"/>) salvo la de Ollama, que solo puede venir del entorno.
/// </summary>
public static class ChatClientFactory
{
    // El SDK reintenta por su cuenta ante errores transitorios; un solo reintento mantiene el peor caso dentro del tiempo máximo.
    private const int MaxRetries = 1;

    public static IChatClient? Create(AiOptions options) => options.ResolvedProvider switch
    {
        AiProvider.Anthropic => CreateAnthropicClient(options).AsIChatClient(options.Model),
        AiProvider.OpenAI or AiProvider.Gemini => CreateOpenAIClient(options).GetChatClient(options.Model).AsIChatClient(),
        AiProvider.Ollama => new OllamaApiClient(new Uri(options.Endpoint!), options.Model!),
        _ => null,
    };

    internal static AnthropicClient CreateAnthropicClient(AiOptions options)
        => new()
        {
            ApiKey = options.ApiKey,
            MaxRetries = MaxRetries,
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

    internal static OpenAIClientOptions CreateOpenAIClientOptions(AiOptions options)
        => new()
        {
            NetworkTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            RetryPolicy = new ClientRetryPolicy(MaxRetries),
            // Gemini habla el protocolo de OpenAI en una URL fija; OpenAI usa la del SDK (Endpoint sin asignar).
            Endpoint = options.ResolvedProvider == AiProvider.Gemini ? ProviderEndpoints.GeminiOpenAi : null,
        };

    private static OpenAIClient CreateOpenAIClient(AiOptions options)
        => new(new ApiKeyCredential(options.ApiKey!), CreateOpenAIClientOptions(options));
}
