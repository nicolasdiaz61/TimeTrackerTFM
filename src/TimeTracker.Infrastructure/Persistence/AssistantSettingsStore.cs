using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>
/// Almacén PostgreSQL de la configuración del asistente; la fila y su auditoría se confirman en una sola transacción (DD-8, DD-9).
/// Puente transitorio (DD-14, PR 1b a 4b): la clave primaria es <c>company_id</c>, pero mientras no haya filtro de compañía ni
/// contexto de petición el almacén sirve siempre a la compañía por defecto. La PR 4a lo generaliza con la compañía del contexto.
/// </summary>
public sealed class AssistantSettingsStore(TimeTrackerDbContext context) : IAssistantSettingsStore
{
    // Un xmin real nunca es 0 (los identificadores de transacción empiezan en 3): el 0 es el centinela «sin fila» del contrato.
    private const string NoRowToken = "0";

    // Columna de sistema de PostgreSQL usada como token de concurrencia; EF la mapea con este nombre en la configuración.
    private const string XminColumn = "xmin";

    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    // Puente de la PR 1b: la única compañía servida hasta que la 4a tome la compañía del contexto.
    private static readonly Guid BridgeCompanyId = Company.DefaultId;

    private IQueryable<AssistantSettingsRecord> Rows() => context.Set<AssistantSettingsRecord>().Where(row => row.CompanyId == BridgeCompanyId);

    public async Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var read = await Rows()
                .AsNoTracking()
                .Select(row => new { Row = row, Xmin = EF.Property<uint>(row, XminColumn) })
                .FirstOrDefaultAsync(cancellationToken);
            return read is null ? null : ToSettings(read.Row, read.Xmin);
        }
        catch (PostgresException exception) when (IsMissingTable(exception))
        {
            return null;
        }
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var xmin = await Rows()
                .AsNoTracking()
                .Select(row => (uint?)EF.Property<uint>(row, XminColumn))
                .FirstOrDefaultAsync(cancellationToken);
            return xmin is { } value ? Token(value) : NoRowToken;
        }
        catch (PostgresException exception) when (IsMissingTable(exception))
        {
            return NoRowToken;
        }
    }

    public async Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
    {
        if (await TrackRowForSaveAsync(expectedToken, cancellationToken) is not { } row)
        {
            return new SaveOutcome.Conflict();
        }

        Apply(next, row);
        context.Add(ToAuditRecord(audit));
        return await CommitSaveAsync(row, expectedToken == NoRowToken, cancellationToken);
    }

    public async Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
    {
        if (await Rows().FirstOrDefaultAsync(cancellationToken) is not { } row)
        {
            return DeleteOutcome.NotFound;
        }

        if (Token(context.Entry(row).Property<uint>(XminColumn).CurrentValue) != expectedToken)
        {
            return DeleteOutcome.Conflict;
        }

        context.Remove(row);
        context.Add(ToAuditRecord(audit));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Cero filas afectadas: o la borró otro Manager (mismo estado final pedido, idempotente) o la cambió (hay que avisar).
            context.ChangeTracker.Clear();
            return await Rows().AnyAsync(cancellationToken) ? DeleteOutcome.Conflict : DeleteOutcome.NotFound;
        }

        return DeleteOutcome.Deleted;
    }

    // 42P01: la tabla aún no existe (el sondeo corrió antes que las migraciones o se revirtieron); equivale a «sin fila» y el asistente usa el entorno.
    private static bool IsMissingTable(PostgresException exception) => exception.SqlState == PostgresErrorCodes.UndefinedTable;

    /// <summary>Fila a modificar con el token esperado como valor original; <c>null</c> si el token no corresponde a la fila actual.</summary>
    private async Task<AssistantSettingsRecord?> TrackRowForSaveAsync(string expectedToken, CancellationToken cancellationToken)
    {
        if (expectedToken == NoRowToken)
        {
            var created = new AssistantSettingsRecord { CompanyId = BridgeCompanyId };
            context.Add(created);
            return created;
        }

        if (!uint.TryParse(expectedToken, NumberStyles.None, CultureInfo.InvariantCulture, out var expected)
            || await Rows().FirstOrDefaultAsync(cancellationToken) is not { } existing)
        {
            return null;
        }

        context.Entry(existing).Property<uint>(XminColumn).OriginalValue = expected;
        return existing;
    }

    private async Task<SaveOutcome> CommitSaveAsync(AssistantSettingsRecord row, bool creating, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SaveOutcome.Conflict();
        }
        catch (DbUpdateException exception) when (creating && exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new SaveOutcome.Conflict();
        }

        return new SaveOutcome.Saved(Token(context.Entry(row).Property<uint>(XminColumn).CurrentValue));
    }

    private static string Token(uint xmin) => xmin.ToString(CultureInfo.InvariantCulture);

    private static void Apply(AssistantSettings settings, AssistantSettingsRecord row)
    {
        row.Provider = settings.Provider.ToString();
        row.Model = settings.Model;
        row.TimeoutSeconds = settings.TimeoutSeconds;
        row.MaxOutputTokens = settings.MaxOutputTokens;
        row.Effort = EffortName(settings.Effort);
        row.CredentialCiphertext = settings.Credential?.Ciphertext;
        row.CredentialNonce = settings.Credential?.Nonce;
        row.CredentialKeyVersion = settings.Credential?.KeyVersion;
        row.UpdatedBy = settings.UpdatedBy;
        row.UpdatedAt = settings.UpdatedAt.ToUniversalTime();
    }

    private static AssistantSettings ToSettings(AssistantSettingsRecord row, uint xmin)
        => new(
            Enum.Parse<AssistantProvider>(row.Provider),
            row.Model,
            row.TimeoutSeconds,
            row.MaxOutputTokens,
            row.Effort is null ? null : Enum.Parse<AssistantEffort>(row.Effort, ignoreCase: true),
            row.CredentialCiphertext is { } ciphertext && row.CredentialNonce is { } nonce && row.CredentialKeyVersion is { } keyVersion
                ? new EncryptedCredential(ciphertext, nonce, keyVersion)
                : null,
            row.UpdatedBy,
            row.UpdatedAt,
            Token(xmin));

    private static AssistantSettingsAuditRecord ToAuditRecord(AuditEntry audit)
        => new()
        {
            CompanyId = BridgeCompanyId,
            OccurredAt = audit.OccurredAt.ToUniversalTime(),
            ActorId = audit.ActorId,
            Action = audit.Action.ToString(),
            CredentialChanged = audit.CredentialChanged,
            Previous = Serialize(audit.Previous),
            Current = Serialize(audit.Current),
        };

    // Solo los valores no secretos de AuditSnapshot: la clave (en claro o cifrada) nunca llega a este JSON.
    private static string? Serialize(AuditSnapshot? snapshot)
        => snapshot is null
            ? null
            : JsonSerializer.Serialize(
                new
                {
                    provider = snapshot.Provider.ToString(),
                    model = snapshot.Model,
                    timeoutSeconds = snapshot.TimeoutSeconds,
                    maxOutputTokens = snapshot.MaxOutputTokens,
                    effort = EffortName(snapshot.Effort),
                    credentialConfigured = snapshot.CredentialConfigured,
                },
                AuditJson);

    private static string? EffortName(AssistantEffort? effort) => effort?.ToString().ToLowerInvariant();
}
