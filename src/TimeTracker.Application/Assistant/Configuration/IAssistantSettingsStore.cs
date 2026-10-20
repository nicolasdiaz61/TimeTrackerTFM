namespace TimeTracker.Application.Assistant.Configuration;

public enum AuditAction
{
    Saved,
    Reset,
}

/// <summary>Valores no secretos de la configuración guardada; es lo único que llega a la auditoría.</summary>
public sealed record AuditSnapshot(
    AssistantProvider Provider,
    string? Model,
    int? TimeoutSeconds,
    int? MaxOutputTokens,
    AssistantEffort? Effort,
    bool CredentialConfigured)
{
    public static AuditSnapshot? From(AssistantSettings? settings)
        => settings is null
            ? null
            : new AuditSnapshot(settings.Provider, settings.Model, settings.TimeoutSeconds, settings.MaxOutputTokens, settings.Effort, settings.Credential is not null);
}

public sealed record AuditEntry(
    Guid ActorId,
    DateTimeOffset OccurredAt,
    AuditAction Action,
    bool CredentialChanged,
    AuditSnapshot? Previous,
    AuditSnapshot? Current);

public abstract record SaveOutcome
{
    private SaveOutcome()
    {
    }

    public sealed record Saved(string Token) : SaveOutcome;

    public sealed record Conflict : SaveOutcome;
}

/// <summary>Resultado del borrado: <c>Conflict</c> si la fila cambió desde la lectura (no se borra ni se audita).</summary>
public enum DeleteOutcome
{
    Deleted,
    NotFound,
    Conflict,
}

/// <summary>Almacén de la fila única de configuración. La auditoría solo admite inserción: se escribe en la misma transacción.</summary>
public interface IAssistantSettingsStore
{
    /// <summary>Devuelve la fila, o <c>null</c> si no existe (también si la tabla aún no existe).</summary>
    Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken);

    /// <summary>Token de concurrencia actual; «0» si no hay fila.</summary>
    Task<string> GetVersionAsync(CancellationToken cancellationToken);

    Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken cancellationToken);

    /// <summary>Borra la fila con el token leído y audita solo si la borra. Sin fila: <c>NotFound</c> (ya está en el estado pedido); con fila y token distinto del actual: <c>Conflict</c>.</summary>
    Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken cancellationToken);
}
