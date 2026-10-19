using System.Diagnostics.CodeAnalysis;

namespace TimeTracker.Application.Assistant.Configuration;

public sealed class CredentialProtectionUnavailableException()
    : InvalidOperationException("No hay una clave maestra utilizable para proteger credenciales.");

public interface IAssistantCredentialProtector
{
    /// <summary>Hay una clave maestra activa y válida.</summary>
    bool IsAvailable { get; }

    /// <exception cref="CredentialProtectionUnavailableException">No hay clave maestra activa utilizable.</exception>
    EncryptedCredential Protect(AssistantCredential plaintext, AssistantProvider provider);

    bool TryUnprotect(EncryptedCredential stored, AssistantProvider provider, [NotNullWhen(true)] out AssistantCredential? plaintext);
}
