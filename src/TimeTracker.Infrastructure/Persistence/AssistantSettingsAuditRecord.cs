using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>Fila de auditoría de solo inserción; <c>Previous</c> y <c>Current</c> son JSON con valores no secretos.</summary>
internal sealed class AssistantSettingsAuditRecord
{
    public long Id { get; set; }

    // Transitorio hasta que la compañía llegue del contexto: la auditoría actual es la de la compañía por defecto.
    public Guid CompanyId { get; set; } = Company.DefaultId;

    public DateTimeOffset OccurredAt { get; set; }

    public Guid ActorId { get; set; }

    public string Action { get; set; } = string.Empty;

    public bool CredentialChanged { get; set; }

    public string? Previous { get; set; }

    public string? Current { get; set; }
}
