namespace TimeTracker.Application.Assistant;

/// <summary>Ámbito que rechazó una petición por cuota: la persona (día), la compañía (día) o la compañía (minuto).</summary>
public enum QuotaScope
{
    User,
    CompanyDaily,
    CompanyMinute,
}

/// <summary>Puerto de los topes del asistente: ritmo por minuto de la compañía y tope diario de la compañía y de la persona (DD-16).</summary>
public interface IAssistantQuota
{
    /// <summary>
    /// Cuenta una petición en la ventana fija de un minuto de la compañía. Se invoca al inicio, tras el límite por usuario, de modo
    /// que también cuentan las peticiones rechazadas después por inválidas o por asistente deshabilitado.
    /// </summary>
    QuotaDecision TryConsumeMinute(Guid companyId);

    /// <summary>
    /// Consume una unidad del día natural de Europe/Madrid de la persona y de su compañía, solo si ambas admiten la petición: un
    /// rechazo de un ámbito no consume el otro.
    /// </summary>
    QuotaDecision TryConsumeDaily(Guid companyId, Guid employeeId);
}

public sealed record QuotaDecision(bool Allowed, TimeSpan RetryAfter, QuotaScope Scope = QuotaScope.User);
