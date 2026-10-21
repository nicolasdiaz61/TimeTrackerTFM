using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>
/// Formato v2 del texto cifrado (DD-17, TC-12): el AAD incluye la compañía, de modo que un texto cifrado de una compañía no se
/// descifra en otra. El formato no se guarda: se deduce probando v2 y, solo para la compañía por defecto, v1.
/// </summary>
public sealed class AesGcmCredentialProtectorV2Tests
{
    private const string Secret = "clave-prueba-123456";
    private const short KeyVersion = 1;

    private static readonly byte[] KeyBytes = RandomNumberGenerator.GetBytes(32);
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-7111-8111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-7222-8222-222222222222");

    private static AesGcmCredentialProtector Create()
    {
        var options = new AiOptions { ActiveMasterKeyVersion = "1" };
        options.MasterKeys["1"] = Convert.ToBase64String(KeyBytes);
        return new AesGcmCredentialProtector(Options.Create(options));
    }

    /// <summary>Cifra como lo hacía el protector anterior a este cambio (AAD v1, sin compañía), con la misma clave maestra.</summary>
    private static EncryptedCredential EncryptAsV1(AssistantProvider provider)
        => Encrypt($"tt-assistant-credential|v1|{provider}|{KeyVersion}");

    /// <summary>Cifra con el AAD v2 esperado por la especificación, escrito aquí de forma independiente del protector.</summary>
    private static EncryptedCredential EncryptAsV2(AssistantProvider provider, Guid companyId)
        => Encrypt($"tt-assistant-credential|v2|{companyId:D}|{provider}|{KeyVersion}");

    private static EncryptedCredential Encrypt(string associatedData)
    {
        var data = Encoding.UTF8.GetBytes(Secret);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var output = new byte[data.Length + 16];
        using var aes = new AesGcm(KeyBytes, 16);
        aes.Encrypt(nonce, data, output.AsSpan(0, data.Length), output.AsSpan(data.Length), Encoding.UTF8.GetBytes(associatedData));
        return new EncryptedCredential(output, nonce, KeyVersion);
    }

    [Fact]
    public void ProtectedForACompany_RoundTripsForThatCompanyAndMatchesTheV2AssociatedData()
    {
        var protector = Create();

        var stored = protector.Protect(new AssistantCredential(Secret), AssistantProvider.Anthropic, CompanyA);

        Assert.True(protector.TryUnprotect(stored, AssistantProvider.Anthropic, CompanyA, out var plaintext));
        Assert.Equal(Secret, plaintext!.Value);
        // El texto cifrado de Protect se puede abrir con el AAD v2 escrito a mano: el formato es el de la especificación.
        Assert.True(CanOpen(stored, $"tt-assistant-credential|v2|{CompanyA:D}|Anthropic|1"));
        Assert.False(CanOpen(stored, "tt-assistant-credential|v1|Anthropic|1"));
    }

    [Fact]
    public void ACiphertextOfCompanyA_IsNotDecryptedWithTheCompanyIdOfB()
    {
        var protector = Create();
        var stored = protector.Protect(new AssistantCredential(Secret), AssistantProvider.Anthropic, CompanyA);

        Assert.False(protector.TryUnprotect(stored, AssistantProvider.Anthropic, CompanyB, out var plaintext));
        Assert.Null(plaintext);
        Assert.True(protector.TryUnprotect(stored, AssistantProvider.Anthropic, CompanyA, out _));
    }

    [Fact]
    public void ACiphertextProtectedForAnotherProvider_IsNotDecryptedEvenInTheSameCompany()
    {
        var protector = Create();
        var stored = protector.Protect(new AssistantCredential(Secret), AssistantProvider.Anthropic, CompanyA);

        Assert.False(protector.TryUnprotect(stored, AssistantProvider.OpenAI, CompanyA, out _));
    }

    [Fact]
    public void ALegacyV1Record_IsReadableOnlyInTheDefaultCompany()
    {
        var protector = Create();
        var legacy = EncryptAsV1(AssistantProvider.Anthropic);

        Assert.True(protector.TryUnprotect(legacy, AssistantProvider.Anthropic, Company.DefaultId, out var plaintext));
        Assert.Equal(Secret, plaintext!.Value);
        Assert.False(protector.TryUnprotect(legacy, AssistantProvider.Anthropic, CompanyA, out var other));
        Assert.Null(other);
    }

    [Fact]
    public void AV2Record_IsAlsoReadableInTheDefaultCompany_WithoutAStoredFormatMarker()
    {
        var protector = Create();
        var v2 = EncryptAsV2(AssistantProvider.Gemini, Company.DefaultId);

        Assert.True(protector.TryUnprotect(v2, AssistantProvider.Gemini, Company.DefaultId, out var plaintext));
        Assert.Equal(Secret, plaintext!.Value);
        Assert.False(protector.TryUnprotect(v2, AssistantProvider.Gemini, CompanyA, out _));
    }

    [Fact]
    public void Protect_AlwaysWritesV2_EvenForTheDefaultCompany()
    {
        var protector = Create();

        var stored = protector.Protect(new AssistantCredential(Secret), AssistantProvider.OpenAI, Company.DefaultId);

        Assert.True(CanOpen(stored, $"tt-assistant-credential|v2|{Company.DefaultId:D}|OpenAI|1"));
        Assert.False(CanOpen(stored, "tt-assistant-credential|v1|OpenAI|1"));
    }

    [Fact]
    public void AnAlteredV2Record_FailsAuthenticationInEveryCompany()
    {
        var protector = Create();
        var stored = protector.Protect(new AssistantCredential(Secret), AssistantProvider.Anthropic, Company.DefaultId);
        var altered = stored.Ciphertext.ToArray();
        altered[0] ^= 1;

        Assert.False(protector.TryUnprotect(stored with { Ciphertext = altered }, AssistantProvider.Anthropic, Company.DefaultId, out var plaintext));
        Assert.Null(plaintext);
    }

    private static bool CanOpen(EncryptedCredential stored, string associatedData)
    {
        var length = stored.Ciphertext.Length - 16;
        var data = new byte[length];
        try
        {
            using var aes = new AesGcm(KeyBytes, 16);
            aes.Decrypt(stored.Nonce, stored.Ciphertext.AsSpan(0, length), stored.Ciphertext.AsSpan(length), data, Encoding.UTF8.GetBytes(associatedData));
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
