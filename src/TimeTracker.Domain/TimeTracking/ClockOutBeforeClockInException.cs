namespace TimeTracker.Domain.TimeTracking;

public sealed class ClockOutBeforeClockInException()
    : DomainRuleException("Hora de salida no válida", "La hora de salida no puede ser anterior a la de entrada.");
