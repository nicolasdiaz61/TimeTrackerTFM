namespace TimeTracker.Infrastructure.Persistence;

/// <summary>
/// Una escritura afecta a una entidad de una compañía distinta de la del contexto (DD-7). Es un defecto del código, no un error
/// del usuario: la API responde un 500 genérico. El mensaje nombra solo el tipo de entidad, nunca datos personales ni identificadores.
/// </summary>
public sealed class CompanyScopeViolationException(string entityType)
    : InvalidOperationException($"La escritura de {entityType} afecta a una compañía distinta de la del contexto y se rechazó.");
