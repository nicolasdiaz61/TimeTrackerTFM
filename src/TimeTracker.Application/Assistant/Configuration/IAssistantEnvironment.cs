namespace TimeTracker.Application.Assistant.Configuration;

public interface IAssistantEnvironment
{
    AssistantEnvironmentDefaults Current { get; }
}

public interface IAssistantRuntimeReloader
{
    Task ReloadAsync(CancellationToken cancellationToken);
}

internal static class AssistantRuntimeReloaderExtensions
{
    /// <summary>El cambio ya está confirmado: si la recarga falla no se deshace, el sondeo de versión reintentará y el cargador deja el aviso.</summary>
    public static async Task ReloadAfterCommitAsync(this IAssistantRuntimeReloader reloader, CancellationToken cancellationToken)
    {
        try
        {
            await reloader.ReloadAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }
}
