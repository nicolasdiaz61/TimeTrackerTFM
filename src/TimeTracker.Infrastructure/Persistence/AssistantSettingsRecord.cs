using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>Fila única de configuración del asistente (tabla <c>assistant_settings</c>); el token de concurrencia es <c>xmin</c>.</summary>
internal sealed class AssistantSettingsRecord
{
    public short Id { get; set; } = 1;

    // Transitorio hasta la PR 1b, donde pasa a ser la clave primaria: el almacén sigue sirviendo a la compañía por defecto.
    public Guid CompanyId { get; set; } = Company.DefaultId;

    public string Provider { get; set; } = string.Empty;

    public string? Model { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? MaxOutputTokens { get; set; }

    public string? Effort { get; set; }

    public byte[]? CredentialCiphertext { get; set; }

    public byte[]? CredentialNonce { get; set; }

    public short? CredentialKeyVersion { get; set; }

    public Guid UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
