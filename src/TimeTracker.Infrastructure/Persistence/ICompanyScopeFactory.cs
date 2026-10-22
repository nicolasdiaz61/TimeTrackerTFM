using Microsoft.Extensions.DependencyInjection;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>
/// Única vía para actuar como una compañía fuera de una petición autenticada (siembra, cargador del asistente). Funciona como un
/// localizador de servicios acotado (excepción a AGENTS §3, ADR 0020): una prueba de arquitectura limita a una lista cerrada
/// los llamadores en <c>src/</c>.
/// </summary>
public interface ICompanyScopeFactory
{
    AsyncServiceScope CreateScope(Guid companyId);
}
