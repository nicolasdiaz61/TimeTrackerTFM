using Microsoft.Extensions.Logging;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Aplica en esta instancia la configuración que acaba de guardar o restablecer un Manager. Si falla, deja un aviso sin
/// secretos y relanza: el guardado ya está confirmado, el caso de uso responde igualmente y el sondeo reintenta (DD-11).
/// </summary>
internal sealed class AssistantRuntimeReloader(IAssistantSettingsLoader loader, ILogger<AssistantRuntimeReloader> logger) : IAssistantRuntimeReloader
{
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
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
