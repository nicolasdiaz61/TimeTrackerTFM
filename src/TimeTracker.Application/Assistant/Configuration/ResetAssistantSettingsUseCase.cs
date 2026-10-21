using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Assistant.Configuration;

public sealed class ResetAssistantSettingsUseCase(
    IAssistantSettingsStore store,
    IAssistantRuntimeReloader reloader,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    /// <summary>Borra la configuración guardada de la compañía y vuelve al valor por defecto de su tipo. Sin fila no audita ni recarga; si otro Manager guardó tras la lectura, no borra (<c>Conflict</c>).</summary>
    public async Task<DeleteOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        var previous = await store.GetAsync(cancellationToken);
        if (previous is null)
        {
            return DeleteOutcome.NotFound;
        }

        var audit = new AuditEntry(
            currentUser.EmployeeId,
            timeProvider.GetUtcNow(),
            AuditAction.Reset,
            CredentialChanged: previous.Credential is not null,
            AuditSnapshot.From(previous),
            Current: null);

        var outcome = await store.DeleteAsync(previous.Version, audit, cancellationToken);
        if (outcome == DeleteOutcome.Deleted)
        {
            await reloader.ReloadAfterCommitAsync(currentUser.CompanyId, cancellationToken);
        }

        return outcome;
    }
}
