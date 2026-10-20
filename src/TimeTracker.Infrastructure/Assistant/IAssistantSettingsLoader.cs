namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Resultado de una carga: <c>Unchanged</c> si la versión ya estaba aplicada, <c>Rejected</c> si la fila no es utilizable y se conserva la última válida.</summary>
public enum SettingsLoadOutcome
{
    Unchanged,
    Applied,
    Rejected,
}

/// <summary>Costura pública entre el sondeo (registrado por Api) y el cargador de la configuración guardada (DD-11).</summary>
public interface IAssistantSettingsLoader
{
    /// <summary>Versión (<c>xmin</c>) de la fila guardada, o <c>"0"</c> si no hay fila. Un fallo de la base de datos se propaga.</summary>
    Task<string> ReadVersionAsync(CancellationToken cancellationToken);

    /// <summary>Lee la fila fresca, la revalida y, solo si es utilizable, publica la instantánea nueva. Un fallo de la base de datos se propaga.</summary>
    Task<SettingsLoadOutcome> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Última versión rechazada y aún vigente en la base de datos; evita volver a leerla y a avisar en cada sondeo.</summary>
    string? LastRejectedVersion { get; }
}
