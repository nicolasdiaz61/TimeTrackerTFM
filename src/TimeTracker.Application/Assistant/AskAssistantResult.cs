namespace TimeTracker.Application.Assistant;

public abstract record AskAssistantResult
{
    private AskAssistantResult()
    {
    }

    public sealed record Answered(string Reply) : AskAssistantResult;

    /// <summary>Petición inválida; <paramref name="Error"/> es un texto fijo que nunca repite lo enviado por el cliente.</summary>
    public sealed record Rejected(string Error) : AskAssistantResult;

    public sealed record QuotaExceeded(TimeSpan RetryAfter, QuotaScope Scope) : AskAssistantResult;

    /// <summary>El asistente no responde: <paramref name="Disabled"/> si está deshabilitado por configuración, falso si falla el proveedor.</summary>
    public sealed record Unavailable(bool Disabled) : AskAssistantResult;
}
