using Microsoft.Extensions.Logging;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Aplica en esta instancia la configuración que acaba de guardar o restablecer un Manager de una compañía. Si falla, deja un
/// aviso sin secretos y relanza: el guardado ya está confirmado, el caso de uso responde igualmente y el sondeo reintenta (DD-11).
/// Puente transitorio (DD-14): el runtime singleton solo guarda la instantánea de la compañía de plataforma (la por defecto), así
/// que el cambio de cualquier otra compañía no tiene nada que recargar y se aplica al leer su fila; la 4b lo sustituye por el
/// registro de instantáneas por compañía.
/// </summary>
internal sealed class AssistantRuntimeReloader(IAssistantSettingsLoader loader, ILogger<AssistantRuntimeReloader> logger) : IAssistantRuntimeReloader
{
    public async Task ReloadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (companyId != Company.DefaultId)
        {
            return;
        }

        try
        {
            await loader.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            AssistantSettingsLog.ReloadFailed(logger, exception.GetType().Name);
            throw;
        }
    }
}
