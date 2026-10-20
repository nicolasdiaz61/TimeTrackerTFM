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
    // Los plazos son por intento, no totales: con un reintento el peor caso de Anthropic/OpenAI/Gemini es ≈ 2 × TimeoutSeconds
    // más la espera entre intentos. El tope global lo impone ChatAssistantEngine con un CancellationTokenSource de
    // TimeoutSeconds (25 s por defecto) enlazado al token de la petición, que corta también los reintentos en curso.
    // Ollama no recibe reintento: OllamaSharp no lo ofrece y un servidor local caído no se arregla reintentando.
    private const int MaxRetries = 1;

    public static IChatClient? Create(AiOptions options)
    {
        // Se revalida aquí y no solo al arrancar: la PR 5 crea clientes con valores resueltos en caliente, que pueden venir incompletos.
        EnsureRequiredValues(options);
        return options.ResolvedProvider switch
        {
            AiProvider.Anthropic => CreateAnthropicClient(options).AsIChatClient(options.Model),
            AiProvider.OpenAI or AiProvider.Gemini => CreateOpenAIClient(options).GetChatClient(options.Model).AsIChatClient(),
            AiProvider.Ollama => CreateOllamaClient(options),
            _ => null,
        };
    }

    // El mensaje nombra el valor que falta y nunca su contenido: la clave no debe llegar a un registro.
    private static void EnsureRequiredValues(AiOptions options)
    {
        if (options.ResolvedProvider is not ({ } provider and not AiProvider.None))
        {
            return;
        }

        var required = provider == AiProvider.Ollama
            ? new (string Name, string? Value)[] { (nameof(AiOptions.Model), options.Model), (nameof(AiOptions.Endpoint), options.Endpoint) }
            : [(nameof(AiOptions.Model), options.Model), (nameof(AiOptions.ApiKey), options.ApiKey)];

        foreach (var (name, value) in required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Falta Ai:{name}, requerido por el proveedor {provider}.");
            }
        }
    }

    internal static IChatClient CreateOllamaClient(AiOptions options, HttpMessageHandler? handler = null)
    {
        var http = CreateOllamaHttpClient(options, handler);
        return new OwnedHttpClientChatClient(new OllamaApiClient(http, options.Model!), http);
    }

    // OllamaSharp no expone el plazo si crea él el HttpClient, y no libera uno ajeno: este se libera con el cliente de chat.
    // Sin seguir redirecciones: un 307 reenviaría la conversación a un host distinto del Endpoint elegido por el entorno.
    internal static HttpClient CreateOllamaHttpClient(AiOptions options, HttpMessageHandler? handler = null)
        => new(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(options.Endpoint!),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

    internal static AnthropicClient CreateAnthropicClient(AiOptions options)
        => new()
        {
            ApiKey = options.ApiKey,
            BaseUrl = ProviderEndpoints.Anthropic.ToString(),
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
