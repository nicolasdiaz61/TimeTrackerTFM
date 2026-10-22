using TimeTracker.Domain;

namespace TimeTracker.Application.Users;

/// <summary>La fila del usuario cambió entre su lectura y su guardado (token de concurrencia): no se pisa el cambio ajeno.</summary>
public sealed class ConcurrentUserChangeException()
    : DomainRuleException("La cuenta ha cambiado", "La cuenta ha cambiado mientras se procesaba la operación. Inténtelo de nuevo.");
