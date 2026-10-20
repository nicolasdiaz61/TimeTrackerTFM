using Microsoft.Extensions.AI;

using TimeTracker.Application.Assistant.Configuration;

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
                client.FunctionInvoker = InvokeToolAsync;
            })
            .Build();

    // Solo se envuelven los fallos de la herramienta: el SDK los relanza tal cual al superar los errores tolerados, y así
    // el motor los distingue por su origen (no por el tipo de excepción) de un fallo del proveedor.
    private static async ValueTask<object?> InvokeToolAsync(FunctionInvocationContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AssistantToolFailureException(exception);
        }
    }

    public static ChatOptions CreateChatOptions(AiOptions options, IEnumerable<AITool> tools)
        => new()
        {
            Tools = [.. tools],
            ToolMode = ChatToolMode.Auto,
            MaxOutputTokens = options.MaxOutputTokens,
            ModelId = options.Model,
            // Sin esfuerzo no se define Reasoning: los modelos que siempre piensan rechazan un valor explícito (DD-12).
            Reasoning = options.ResolvedEffort is { } effort ? new ReasoningOptions { Effort = ToReasoningEffort(effort) } : null,
        };

    private static ReasoningEffort ToReasoningEffort(AssistantEffort effort) => effort switch
    {
        AssistantEffort.Low => ReasoningEffort.Low,
        AssistantEffort.Medium => ReasoningEffort.Medium,
        _ => ReasoningEffort.High,
    };
}
