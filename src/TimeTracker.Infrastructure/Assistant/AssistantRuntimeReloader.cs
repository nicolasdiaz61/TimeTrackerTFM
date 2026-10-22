using Microsoft.Extensions.Logging;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Aplica en esta instancia la configuración que acaba de guardar o restablecer un Manager de una compañía. Si falla, deja un
/// aviso sin secretos y relanza: el guardado ya está confirmado, el caso de uso responde igualmente y el sondeo reintenta (DD-14).
/// Si la compañía no está cargada no hay nada que recargar: cargará su fila fresca en la siguiente pregunta.
/// </summary>
internal sealed class AssistantRuntimeReloader(IAssistantRuntimeRegistry registry, ILogger<AssistantRuntimeReloader> logger) : IAssistantRuntimeReloader
{
    public async Task ReloadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        try
        {
            await registry.ReloadAsync(companyId, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            AssistantSettingsLog.ReloadFailed(logger, exception.GetType().Name);
            throw;
        }
    }
}
