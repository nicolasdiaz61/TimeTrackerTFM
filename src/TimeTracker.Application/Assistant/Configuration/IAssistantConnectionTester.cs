namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Valores ya validados y completos con los efectivos; la clave candidata solo vive durante la prueba.</summary>
public sealed record AssistantConnectionCandidate(
    AssistantProvider Provider,
    string Model,
    int TimeoutSeconds,
    int MaxOutputTokens,
    AssistantEffort? Effort,
    AssistantCredential? Credential);

/// <summary><paramref name="Result"/> es una categoría fija (<c>ok</c>, <c>authentication</c>...); <paramref name="Message"/> un texto fijo en español.</summary>
public sealed record ConnectionTestResult(string Result, string Message);

public interface IAssistantConnectionTester
{
    Task<ConnectionTestResult> TestAsync(AssistantConnectionCandidate candidate, CancellationToken cancellationToken);
}
