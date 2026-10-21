using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Convierte la fila guardada en la instantánea del asistente (DD-5, DD-11). Revalida antes de crear el cliente y, si la fila
/// no es utilizable (inválida, no descifrable o con un proveedor ya no permitido), no publica nada: se conserva la última
/// configuración válida y se avisa una vez por versión rechazada. Singleton: serializa las cargas y lee siempre la fila
/// fresca, de modo que no puede publicar una versión más antigua que la ya aplicada.
/// </summary>
internal sealed class AssistantSettingsLoader(
    ICompanyScopeFactory companyScopes,
    IOptions<AiOptions> options,
    IAssistantEnvironment environment,
    IAssistantCredentialProtector protector,
    IChatClientFactory factory,
    AssistantChatClient client,
    ILogger<AssistantSettingsLoader> logger) : IAssistantSettingsLoader
{
    private const string InvalidCategory = "invalid";
    private const string UndecryptableCategory = "undecryptable";
    private const string NotAllowedCategory = "not-allowed";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _lastRejectedVersion;

    public string? LastRejectedVersion => Volatile.Read(ref _lastRejectedVersion);

    // Puente transitorio (DD-14): el runtime sigue siendo un singleton que sirve a la compañía por defecto, hasta la PR 4b.
    private AsyncServiceScope BridgeScope() => companyScopes.CreateScope(Company.DefaultId);

    public async Task<string> ReadVersionAsync(CancellationToken cancellationToken)
    {
        await using var scope = BridgeScope();
        var version = await scope.ServiceProvider.GetRequiredService<IAssistantSettingsStore>().GetVersionAsync(cancellationToken);
        if (version != AssistantSnapshot.EnvironmentVersion)
        {
            return version;
        }

        // Sin fila, la versión depende de si la compañía sigue usando los valores de plataforma.
        return await ReadUsesPlatformAsync(scope.ServiceProvider, cancellationToken) ? version : AssistantSnapshot.DisabledVersion;
    }

    public async Task<SettingsLoadOutcome> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var (stored, usesPlatform) = await ReadBridgeAsync(cancellationToken);
            var version = stored?.Version ?? (usesPlatform ? AssistantSnapshot.EnvironmentVersion : AssistantSnapshot.DisabledVersion);
            if (version == client.Current.Version)
            {
                return SettingsLoadOutcome.Unchanged;
            }

            return stored is null
                ? Apply(usesPlatform ? options.Value : DisabledOptions(), version)
                : ResolveStored(stored, usesPlatform) switch
                {
                    { Options: { } resolved } => Apply(resolved, version),
                    var rejected => Reject(version, rejected.Category!),
                };
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(AssistantSettings? Stored, bool UsesPlatform)> ReadBridgeAsync(CancellationToken cancellationToken)
    {
        await using var scope = BridgeScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IAssistantSettingsStore>().GetAsync(cancellationToken);
        return (stored, await ReadUsesPlatformAsync(scope.ServiceProvider, cancellationToken));
    }

    // Una compañía por defecto que ya no tiene uses_platform_assistant no usa el entorno (proveedor, modelo ni clave). Con la tabla
    // `companies` aún sin crear (el sondeo corre antes que las migraciones) manda el comportamiento de siempre: la demo usa el entorno.
    private static async Task<bool> ReadUsesPlatformAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            var info = await services.GetRequiredService<ICompanyRepository>().FindLoginInfoAsync(Company.DefaultId, cancellationToken);
            return info?.UsesPlatformAssistant ?? false;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return true;
        }
    }

    private AiOptions DisabledOptions()
    {
        var disabled = options.Value.Copy();
        disabled.Provider = AssistantProvider.None.ToString();
        disabled.Model = null;
        disabled.ApiKey = null;
        return disabled;
    }

    /// <summary>Las mismas reglas que el guardado más las del entorno, antes de descifrar con efecto ni crear nada.</summary>
    private (AiOptions? Options, string? Category) ResolveStored(AssistantSettings stored, bool usesPlatform)
    {
        var env = environment.Current.ForCompany(usesPlatform);
        if (stored.Provider != AssistantProvider.None && !AllowedProviders.Resolve(env).Providers.Contains(stored.Provider))
        {
            return (null, NotAllowedCategory);
        }

        AssistantCredential? plain = null;
        if (stored.Provider != AssistantProvider.None && stored.Credential is { } encrypted && !protector.TryUnprotect(encrypted, stored.Provider, Company.DefaultId, out plain))
        {
            return (null, UndecryptableCategory);
        }

        if (!AssistantSettingsRules.Validate(ToInput(stored), env, stored, protector, Company.DefaultId).IsValid)
        {
            return (null, InvalidCategory);
        }

        var effective = AssistantSettingsResolver.Resolve(env, stored, storedCredentialUsable: plain is not null, usesPlatform);
        var resolved = options.Value.Copy();
        resolved.Provider = effective.Provider.ToString();
        resolved.Model = effective.Model;
        resolved.TimeoutSeconds = effective.TimeoutSeconds;
        resolved.MaxOutputTokens = effective.MaxOutputTokens;
        resolved.Effort = effective.Effort?.ToString();
        resolved.ApiKey = effective.Sources.Credential switch
        {
            SettingSource.Stored => plain?.Value,
            SettingSource.Env => env.Credential?.Value,
            _ => null,
        };
        return (resolved, null);
    }

    private static AssistantSettingsInput ToInput(AssistantSettings stored)
        => new(stored.Provider.ToString(), stored.Model, stored.TimeoutSeconds, stored.MaxOutputTokens, stored.Effort?.ToString(), Credential: null);

    private SettingsLoadOutcome Apply(AiOptions resolved, string version)
    {
        IChatClient? inner;
        try
        {
            inner = factory.Create(resolved);
        }
        catch (Exception exception)
        {
            return Reject(version, InvalidCategory, exception.GetType().Name);
        }

        client.Swap(inner, resolved, version);
        Volatile.Write(ref _lastRejectedVersion, null);
        AssistantSettingsLog.Applied(logger, version, resolved.Provider ?? "None");
        return SettingsLoadOutcome.Applied;
    }

    private SettingsLoadOutcome Reject(string version, string category, string? exceptionType = null)
    {
        if (Interlocked.Exchange(ref _lastRejectedVersion, version) != version)
        {
            if (exceptionType is null)
            {
                AssistantSettingsLog.Rejected(logger, version, category);
            }
            else
            {
                AssistantSettingsLog.RejectedWithException(logger, version, category, exceptionType);
            }
        }

        return SettingsLoadOutcome.Rejected;
    }
}
