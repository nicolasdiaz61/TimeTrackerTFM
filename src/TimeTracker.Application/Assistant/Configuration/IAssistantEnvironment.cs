namespace TimeTracker.Application.Assistant.Configuration;

public interface IAssistantEnvironment
{
    AssistantEnvironmentDefaults Current { get; }
}

public interface IAssistantRuntimeReloader
{
    /// <summary>Aplica en esta instancia el cambio de configuración de la compañía indicada (DD-14); no toca a las demás.</summary>
    Task ReloadAsync(Guid companyId, CancellationToken cancellationToken);
}

internal static class AssistantRuntimeReloaderExtensions
{
    /// <summary>El cambio ya está confirmado: si la recarga falla no se deshace, el sondeo de versión reintentará y el cargador deja el aviso.</summary>
    public static async Task ReloadAfterCommitAsync(this IAssistantRuntimeReloader reloader, Guid companyId, CancellationToken cancellationToken)
    {
        try
        {
            await reloader.ReloadAsync(companyId, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }
}
