using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Assistant.Configuration;

public sealed class ResetAssistantSettingsUseCase(
    IAssistantSettingsStore store,
    IAssistantRuntimeReloader reloader,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    /// <summary>Borra la configuración guardada y vuelve al entorno. Devuelve si había fila; sin fila no audita ni recarga.</summary>
    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        var previous = await store.GetAsync(cancellationToken);
        var audit = new AuditEntry(
            currentUser.EmployeeId,
            timeProvider.GetUtcNow(),
            AuditAction.Reset,
            CredentialChanged: previous?.Credential is not null,
            AuditSnapshot.From(previous),
            Current: null);

        if (!await store.DeleteAsync(audit, cancellationToken))
        {
            return false;
        }

        await reloader.ReloadAfterCommitAsync(cancellationToken);
        return true;
    }
}
