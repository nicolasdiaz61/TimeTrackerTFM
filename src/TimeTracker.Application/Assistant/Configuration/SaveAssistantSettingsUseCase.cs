using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Assistant.Configuration;

public sealed record SaveAssistantSettingsCommand(AssistantSettingsInput Input, string? ConcurrencyToken);

public abstract record SaveAssistantSettingsResult
{
    private SaveAssistantSettingsResult()
    {
    }

    public sealed record Saved(AssistantSettingsView View) : SaveAssistantSettingsResult;

    public sealed record Invalid(IReadOnlyList<FieldError> Errors) : SaveAssistantSettingsResult;

    /// <summary>El token no coincide con la fila actual (o alguien creó la fila antes); no se modificó nada.</summary>
    public sealed record Conflict : SaveAssistantSettingsResult;
}

public sealed class SaveAssistantSettingsUseCase(
    IAssistantSettingsStore store,
    IAssistantCredentialProtector protector,
    IAssistantEnvironment environment,
    IAssistantRuntimeReloader reloader,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly FieldError StorageUnavailable = new(
        "credential",
        "No se puede guardar la clave: el almacenamiento de claves no está disponible en este entorno.");

    public async Task<SaveAssistantSettingsResult> ExecuteAsync(SaveAssistantSettingsCommand command, CancellationToken cancellationToken)
    {
        var env = environment.Current;
        var stored = await store.GetAsync(cancellationToken);

        var errors = new List<FieldError>();
        if (AssistantSettingsRules.ValidateToken(command.ConcurrencyToken) is { } tokenError)
        {
            errors.Add(tokenError);
        }

        var validation = AssistantSettingsRules.Validate(command.Input, env, stored, protector, currentUser.CompanyId);
        errors.AddRange(validation.Errors);
        if (validation.Value is not { } value || errors.Count > 0)
        {
            return new SaveAssistantSettingsResult.Invalid(errors);
        }

        if (value.Credential is not null && !protector.IsAvailable)
        {
            return new SaveAssistantSettingsResult.Invalid([StorageUnavailable]);
        }

        var (credential, credentialChanged) = ResolveCredential(value, stored);
        var now = TruncateToMicroseconds(timeProvider.GetUtcNow());
        var next = new AssistantSettings(
            value.Provider, value.Model, value.TimeoutSeconds, value.MaxOutputTokens, value.Effort, credential, currentUser.EmployeeId, now, command.ConcurrencyToken!);
        var audit = new AuditEntry(currentUser.EmployeeId, now, AuditAction.Saved, credentialChanged, AuditSnapshot.From(stored), AuditSnapshot.From(next));

        if (await store.SaveAsync(next, command.ConcurrencyToken!, audit, cancellationToken) is not SaveOutcome.Saved saved)
        {
            return new SaveAssistantSettingsResult.Conflict();
        }

        await reloader.ReloadAfterCommitAsync(cancellationToken);
        return new SaveAssistantSettingsResult.Saved(AssistantSettingsView.Build(env, protector, next with { Version = saved.Token }, currentUser.CompanyId));
    }

    // PostgreSQL guarda microsegundos: sin truncar, el PUT mostraría un instante que el GET siguiente no repite.
    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset instant)
        => instant.AddTicks(-(instant.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));

    /// <summary>Clave nueva: se cifra. Omitida con el mismo proveedor: se conserva (y se recifra si se puede). Otro proveedor: se descarta.</summary>
    private (EncryptedCredential? Credential, bool Changed) ResolveCredential(ValidatedSettingsInput value, AssistantSettings? stored)
    {
        if (value.Provider == AssistantProvider.None)
        {
            return (null, stored?.Credential is not null);
        }

        if (value.Credential is not null)
        {
            return (protector.Protect(value.Credential, value.Provider, currentUser.CompanyId), true);
        }

        if (stored?.Credential is not { } existing || stored.Provider != value.Provider)
        {
            return (null, stored?.Credential is not null);
        }

        return (ReEncryptOrKeep(existing, value.Provider), false);
    }

    /// <summary>Recifrado oportunista con la clave maestra activa; si no se puede (ni descifrar ni cifrar), se conserva la guardada.</summary>
    private EncryptedCredential ReEncryptOrKeep(EncryptedCredential existing, AssistantProvider provider)
    {
        try
        {
            return protector.IsAvailable && protector.TryUnprotect(existing, provider, currentUser.CompanyId, out var plain) ? protector.Protect(plain, provider, currentUser.CompanyId) : existing;
        }
        catch (CredentialProtectionUnavailableException)
        {
            return existing;
        }
    }
}

