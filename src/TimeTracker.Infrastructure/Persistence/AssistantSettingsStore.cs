using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>Almacén PostgreSQL de la fila única de configuración; la fila y su auditoría se confirman en una sola transacción (DD-8, DD-9).</summary>
public sealed class AssistantSettingsStore(TimeTrackerDbContext context) : IAssistantSettingsStore
{
    private const string NoRowToken = "0";

    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    public async Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var read = await context.Set<AssistantSettingsRecord>()
                .AsNoTracking()
                .Select(row => new { Row = row, Xmin = EF.Property<uint>(row, "xmin") })
                .FirstOrDefaultAsync(cancellationToken);
            return read is null ? null : ToSettings(read.Row, read.Xmin);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return null;
        }
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var xmin = await context.Set<AssistantSettingsRecord>()
                .AsNoTracking()
                .Select(row => (uint?)EF.Property<uint>(row, "xmin"))
                .FirstOrDefaultAsync(cancellationToken);
            return xmin is { } value ? Token(value) : NoRowToken;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return NoRowToken;
        }
    }

    public async Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
    {
        AssistantSettingsRecord row;
        if (expectedToken == NoRowToken)
        {
            row = new AssistantSettingsRecord();
            context.Add(row);
        }
        else
        {
            if (!uint.TryParse(expectedToken, NumberStyles.None, CultureInfo.InvariantCulture, out var expected)
                || await context.Set<AssistantSettingsRecord>().FirstOrDefaultAsync(cancellationToken) is not { } existing)
            {
                return new SaveOutcome.Conflict();
            }

            row = existing;
            context.Entry(row).Property<uint>("xmin").OriginalValue = expected;
        }

        Apply(next, row);
        context.Add(ToAuditRecord(audit));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SaveOutcome.Conflict();
        }
        catch (DbUpdateException exception) when (expectedToken == NoRowToken && exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new SaveOutcome.Conflict();
        }

        return new SaveOutcome.Saved(Token(context.Entry(row).Property<uint>("xmin").CurrentValue));
    }

    public async Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
    {
        if (await context.Set<AssistantSettingsRecord>().FirstOrDefaultAsync(cancellationToken) is not { } row)
        {
            return DeleteOutcome.NotFound;
        }

        if (Token(context.Entry(row).Property<uint>("xmin").CurrentValue) != expectedToken)
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
            return DeleteOutcome.Conflict;
        }

        return DeleteOutcome.Deleted;
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
