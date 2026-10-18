namespace TimeTracker.Application.Assistant;

/// <summary>Puerto del tope diario de consultas por empleado.</summary>
public interface IAssistantQuota
{
    /// <summary>Consume una unidad si queda cupo; si no, indica cuánto falta para que se reinicie.</summary>
    QuotaDecision TryConsume(Guid employeeId);
}

public sealed record QuotaDecision(bool Allowed, TimeSpan RetryAfter);
