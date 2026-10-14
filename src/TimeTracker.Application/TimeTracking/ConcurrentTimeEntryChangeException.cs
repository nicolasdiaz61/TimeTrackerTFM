using TimeTracker.Domain;

namespace TimeTracker.Application.TimeTracking;

/// <summary>Otra operación modificó el intervalo abierto (o abrió otro) mientras se cambiaba de estado.</summary>
public sealed class ConcurrentTimeEntryChangeException()
    : DomainRuleException("El fichaje ha cambiado", "Otra operación ha modificado su fichaje. Actualice la página e inténtelo de nuevo.");
