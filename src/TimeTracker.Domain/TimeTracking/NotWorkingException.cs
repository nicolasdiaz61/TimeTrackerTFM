namespace TimeTracker.Domain.TimeTracking;

public sealed class NotWorkingException()
    : DomainRuleException("No está trabajando", "Solo puede iniciar una pausa mientras trabaja. Registre la entrada o termine la pausa en curso.");
