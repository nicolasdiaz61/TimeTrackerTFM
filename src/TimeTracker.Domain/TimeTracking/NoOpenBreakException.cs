namespace TimeTracker.Domain.TimeTracking;

public sealed class NoOpenBreakException()
    : DomainRuleException("No hay ninguna pausa abierta", "Solo puede terminar una pausa si hay una en curso.");
