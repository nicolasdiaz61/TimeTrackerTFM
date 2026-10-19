using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Cifra la clave del proveedor con AES-256-GCM y una clave maestra versionada del entorno (DD-6). Las claves se
/// decodifican y validan una sola vez; una entrada inválida deja solo esa versión como no disponible y nunca se imprime.
/// El texto cifrado guarda la etiqueta al final y se autentica contra proveedor y versión (AAD).
/// </summary>
public sealed class AesGcmCredentialProtector : IAssistantCredentialProtector
{
    private const int MasterKeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly Dictionary<short, byte[]> _masterKeys = [];
    private readonly short? _activeVersion;

    public AesGcmCredentialProtector(IOptions<AiOptions> options)
    {
        foreach (var (version, encoded) in options.Value.MasterKeys)
        {
            if (TryParseVersion(version, out var parsed) && TryDecodeMasterKey(encoded, out var key))
            {
                _masterKeys[parsed] = key;
            }
        }

        if (TryParseVersion(options.Value.ActiveMasterKeyVersion, out var active) && _masterKeys.ContainsKey(active))
        {
            _activeVersion = active;
        }
    }

    public bool IsAvailable => _activeVersion is not null;

    public EncryptedCredential Protect(AssistantCredential plaintext, AssistantProvider provider)
    {
        if (_activeVersion is not { } version)
        {
            throw new CredentialProtectionUnavailableException();
        }

        var data = Encoding.UTF8.GetBytes(plaintext.Value);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var output = new byte[data.Length + TagBytes];
        using var aes = new AesGcm(_masterKeys[version], TagBytes);
        aes.Encrypt(nonce, data, output.AsSpan(0, data.Length), output.AsSpan(data.Length), AssociatedData(provider, version));
        return new EncryptedCredential(output, nonce, version);
    }

    public bool TryUnprotect(EncryptedCredential stored, AssistantProvider provider, [NotNullWhen(true)] out AssistantCredential? plaintext)
    {
        plaintext = null;
        if (!_masterKeys.TryGetValue(stored.KeyVersion, out var key) || stored.Nonce.Length != NonceBytes || stored.Ciphertext.Length < TagBytes)
        {
            return false;
        }

        var cipherLength = stored.Ciphertext.Length - TagBytes;
        var data = new byte[cipherLength];
        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(stored.Nonce, stored.Ciphertext.AsSpan(0, cipherLength), stored.Ciphertext.AsSpan(cipherLength), data, AssociatedData(provider, stored.KeyVersion));
        }
        catch (CryptographicException)
        {
            return false;
        }

        plaintext = new AssistantCredential(Encoding.UTF8.GetString(data));
        return true;
    }

    private static byte[] AssociatedData(AssistantProvider provider, short version)
        => Encoding.UTF8.GetBytes($"tt-assistant-credential|v1|{provider}|{version}");

    private static bool TryParseVersion(string? value, out short version)
        => short.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out version) && version >= 1;

    private static bool TryDecodeMasterKey(string? encoded, [NotNullWhen(true)] out byte[]? key)
    {
        key = null;
        Span<byte> buffer = stackalloc byte[MasterKeyBytes + 3];
        if (string.IsNullOrEmpty(encoded) || !Convert.TryFromBase64String(encoded, buffer, out var written) || written != MasterKeyBytes)
        {
            return false;
        }

        key = buffer[..written].ToArray();
        return true;
    }
}
