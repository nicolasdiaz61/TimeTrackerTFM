using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Prueba un candidato con un cliente temporal (DD-13): un único mensaje fijo sin datos del usuario, sin herramientas, con pocos
/// tokens y un plazo acotado. No toca la instantánea del chat, no guarda nada y solo devuelve una categoría fija.
/// </summary>
internal sealed class AssistantConnectionTester(
    IChatClientFactory factory,
    IOptions<AiOptions> environment,
    TimeProvider timeProvider,
    ILogger<AssistantConnectionTester> logger) : IAssistantConnectionTester
{
    internal const string Prompt = "Responde solo: OK.";
    internal const int MaxOutputTokens = 64;
    internal static readonly TimeSpan MaxDeadline = TimeSpan.FromSeconds(15);

    public async Task<ConnectionTestResult> TestAsync(AssistantConnectionCandidate candidate, CancellationToken cancellationToken)
    {
        var provider = candidate.Provider.ToString();
        var options = ToOptions(candidate);
        var deadline = TimeSpan.FromSeconds(candidate.TimeoutSeconds) < MaxDeadline ? TimeSpan.FromSeconds(candidate.TimeoutSeconds) : MaxDeadline;
        using var timeout = new CancellationTokenSource(deadline, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            using var client = factory.Create(options) ?? throw new InvalidOperationException("La fábrica no creó un cliente para un proveedor real.");
            await client.GetResponseAsync(Prompt, AssistantChatPipeline.CreateChatOptions(options, []), linked.Token);
            AssistantConnectionTestLog.Succeeded(logger, provider, ConnectionTestResults.Ok.Result);
            return ConnectionTestResults.Ok;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var result = ConnectionFailureClassifier.Classify(exception, timeout.IsCancellationRequested);
            AssistantConnectionTestLog.Failed(logger, provider, result.Result, exception.GetType().Name);
            return result;
        }
    }

    // Del entorno solo se toma lo que el candidato no define (el endpoint de Ollama): la clave y los valores son los del candidato.
    private AiOptions ToOptions(AssistantConnectionCandidate candidate)
    {
        var options = environment.Value.Copy();
        options.Provider = candidate.Provider.ToString();
        options.Model = candidate.Model;
        options.TimeoutSeconds = candidate.TimeoutSeconds;
        options.MaxOutputTokens = MaxOutputTokens;
        options.Effort = candidate.Effort?.ToString();
        options.ApiKey = candidate.Credential?.Value;
        return options;
    }
}
