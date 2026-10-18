using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Monta el cliente de chat con el bucle de herramientas acotado (DD-10). No se usan los valores por defecto del SDK:
/// el bucle tiene tope, es secuencial (las herramientas comparten el <c>DbContext</c> del ámbito), no detalla los
/// errores de herramienta al modelo y no registra argumentos (sin <c>loggerFactory</c>).
/// </summary>
internal static class AssistantChatPipeline
{
    // El SDK tolera este número de iteraciones fallidas seguidas y relanza la excepción original en la siguiente.
    private const int ToleratedConsecutiveToolErrors = 2;

    public static IChatClient Build(IChatClient inner, AiOptions options)
        => new ChatClientBuilder(inner)
            .UseFunctionInvocation(loggerFactory: null, client =>
            {
                client.MaximumIterationsPerRequest = options.MaxToolIterations;
                client.MaximumConsecutiveErrorsPerRequest = ToleratedConsecutiveToolErrors;
                client.IncludeDetailedErrors = false;
                client.AllowConcurrentInvocation = false;
                client.TerminateOnUnknownCalls = false;
            })
            .Build();

    public static ChatOptions CreateChatOptions(AiOptions options, IEnumerable<AITool> tools)
        => new()
        {
            Tools = [.. tools],
            ToolMode = ChatToolMode.Auto,
            MaxOutputTokens = options.MaxOutputTokens,
            ModelId = options.Model,
        };
}
