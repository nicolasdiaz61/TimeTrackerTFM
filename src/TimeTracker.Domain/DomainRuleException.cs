namespace TimeTracker.Domain;

/// <summary>
/// Base de las infracciones de reglas de negocio. La capa de entrada las traduce a un conflicto (409)
/// usando <see cref="Title"/> y <see cref="Exception.Message"/>, ambos en español y sin datos internos.
/// </summary>
public abstract class DomainRuleException(string title, string message) : Exception(message)
{
    public string Title { get; } = title;
}
