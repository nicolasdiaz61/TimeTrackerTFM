namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>
/// Única regla de «clave utilizable»: la guardada cuenta si existe, es del mismo proveedor y se puede descifrar;
/// la del entorno, si es del mismo proveedor. Validar, resolver y probar parten siempre de aquí.
/// </summary>
internal static class UsableCredential
{
    public static AssistantCredential? FromStored(AssistantSettings? stored, AssistantProvider provider, IAssistantCredentialProtector protector, Guid companyId)
        => stored is { Credential: { } encrypted } && stored.Provider == provider && protector.TryUnprotect(encrypted, provider, companyId, out var plain)
            ? plain
            : null;

    public static AssistantCredential? FromEnvironment(AssistantEnvironmentDefaults environment, AssistantProvider provider)
        => environment.Provider == provider ? environment.Credential : null;
}
