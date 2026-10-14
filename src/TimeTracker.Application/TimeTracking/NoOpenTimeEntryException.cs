using TimeTracker.Domain;

namespace TimeTracker.Application.TimeTracking;

public sealed class NoOpenTimeEntryException()
    : DomainRuleException("No hay ningún fichaje abierto", "Registre la entrada antes de fichar la salida.");
