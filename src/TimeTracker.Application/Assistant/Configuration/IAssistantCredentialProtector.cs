using System.Diagnostics.CodeAnalysis;

namespace TimeTracker.Application.Assistant.Configuration;

public sealed class CredentialProtectionUnavailableException()
    : InvalidOperationException("No hay una clave maestra utilizable para proteger credenciales.");

public interface IAssistantCredentialProtector
{
    /// <summary>Hay una clave maestra activa y válida.</summary>
    bool IsAvailable { get; }

    /// <summary>Por qué no está disponible (<c>None</c> si lo está); categórico, sin valores ni versiones.</summary>
    ProtectionUnavailableReason UnavailableReason { get; }

    /// <summary>Cifra siempre en formato v2: el texto cifrado queda vinculado a la compañía y al proveedor (DD-17).</summary>
    /// <exception cref="CredentialProtectionUnavailableException">No hay clave maestra activa utilizable.</exception>
    EncryptedCredential Protect(AssistantCredential plaintext, AssistantProvider provider, Guid companyId);

    /// <summary>
    /// Descifra probando v2 y, solo para la compañía por defecto, el formato v1 anterior (el formato no se guarda). Un texto cifrado
    /// de otra compañía, de otro proveedor o manipulado no se descifra.
    /// </summary>
    bool TryUnprotect(EncryptedCredential stored, AssistantProvider provider, Guid companyId, [NotNullWhen(true)] out AssistantCredential? plaintext);
}
