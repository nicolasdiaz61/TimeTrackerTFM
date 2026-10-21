namespace TimeTracker.Infrastructure.Persistence;

/// <summary>Fila de configuración del asistente de una compañía (tabla <c>assistant_settings</c>, clave primaria <c>company_id</c>); el token de concurrencia es <c>xmin</c>.</summary>
internal sealed class AssistantSettingsRecord
{
    public Guid CompanyId { get; set; }

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
