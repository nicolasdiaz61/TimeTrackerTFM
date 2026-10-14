namespace TimeTracker.Domain.TimeTracking;

public sealed class TimeEntryAlreadyClosedException()
    : DomainRuleException("El fichaje ya está cerrado", "El fichaje ya tiene registrada la salida.");
