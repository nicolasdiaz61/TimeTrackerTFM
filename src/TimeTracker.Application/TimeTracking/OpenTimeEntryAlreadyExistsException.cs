using TimeTracker.Domain;

namespace TimeTracker.Application.TimeTracking;

public sealed class OpenTimeEntryAlreadyExistsException()
    : DomainRuleException("Ya existe un fichaje abierto", "Registre la salida antes de fichar una nueva entrada.");
