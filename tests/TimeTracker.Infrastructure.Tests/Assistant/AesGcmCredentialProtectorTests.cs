using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AesGcmCredentialProtectorTests
{
    private const string Secret = "clave-prueba-123456";

    private static readonly string KeyOne = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string KeyTwo = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmCredentialProtector Create(string? active, params (string Version, string? Key)[] keys)
    {
        var options = new AiOptions { ActiveMasterKeyVersion = active };
        foreach (var (version, key) in keys)
        {
            options.MasterKeys[version] = key;
        }

        return new AesGcmCredentialProtector(Options.Create(options));
    }

    private static EncryptedCredential Protect(AesGcmCredentialProtector protector, AssistantProvider provider = AssistantProvider.Anthropic)
        => protector.Protect(new AssistantCredential(Secret), provider);

    [Fact]
    public void RoundTrip_RestoresTheKeyAndTheStoredBytesDoNotContainIt()
    {
        var protector = Create("1", ("1", KeyOne));

        var stored = Protect(protector);

        Assert.True(protector.IsAvailable);
        Assert.True(protector.TryUnprotect(stored, AssistantProvider.Anthropic, out var plaintext));
        Assert.Equal(Secret, plaintext!.Value);
        Assert.Equal((short)1, stored.KeyVersion);
        Assert.Equal(12, stored.Nonce.Length);
        Assert.DoesNotContain(Secret, Encoding.UTF8.GetString(stored.Ciphertext), StringComparison.Ordinal);
    }

    [Fact]
    public void ProtectingTwice_ProducesDifferentNoncesAndCiphertexts()
    {
        var protector = Create("1", ("1", KeyOne));

        var first = Protect(protector);
        var second = Protect(protector);

        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.NotEqual(first.Ciphertext, second.Ciphertext);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void AlteredByte_FailsAuthenticationWithoutReturningAKey(int index)
    {
        var protector = Create("1", ("1", KeyOne));
        var stored = Protect(protector);
        var altered = stored.Ciphertext.ToArray();
        altered[index] ^= 1;

        Assert.False(protector.TryUnprotect(stored with { Ciphertext = altered }, AssistantProvider.Anthropic, out var plaintext));
        Assert.Null(plaintext);
    }

    [Fact]
    public void TruncatedOrMalformedRecord_IsNotDecryptable()
    {
        var protector = Create("1", ("1", KeyOne));
        var stored = Protect(protector);

        Assert.False(protector.TryUnprotect(stored with { Ciphertext = stored.Ciphertext[..8] }, AssistantProvider.Anthropic, out _));
        Assert.False(protector.TryUnprotect(stored with { Nonce = stored.Nonce[..4] }, AssistantProvider.Anthropic, out _));
    }

    [Fact]
    public void AnotherProvider_FailsAuthentication()
    {
        var protector = Create("1", ("1", KeyOne));
        var stored = Protect(protector, AssistantProvider.Anthropic);

        Assert.False(protector.TryUnprotect(stored, AssistantProvider.OpenAI, out var plaintext));
        Assert.Null(plaintext);
    }

    [Fact]
    public void AnotherKeyVersionWithTheSameKeyMaterial_FailsAuthentication()
    {
        var protector = Create("1", ("1", KeyOne), ("2", KeyOne));
        var stored = Protect(protector);

        Assert.False(protector.TryUnprotect(stored with { KeyVersion = 2 }, AssistantProvider.Anthropic, out _));
    }

    [Theory]
    [InlineData("esto-no-es-base64-secreto")]
    [InlineData("")]
    [InlineData("YWJj")]
    public void InvalidMasterKey_MakesThatVersionUnavailableWithoutPrintingTheValue(string key)
    {
        var protector = Create("1", ("1", key));

        Assert.False(protector.IsAvailable);
        var failure = Assert.Throws<CredentialProtectionUnavailableException>(() => Protect(protector));
        Assert.DoesNotContain(key.Length == 0 ? "\0" : key, failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("32768")]
    [InlineData("abc")]
    public void MissingOrOutOfRangeActiveVersion_IsUnavailableAndProtectFailsClosed(string? active)
    {
        var protector = Create(active, ("1", KeyOne), ("0", KeyOne), ("32768", KeyOne));

        Assert.False(protector.IsAvailable);
        Assert.Throws<CredentialProtectionUnavailableException>(() => Protect(protector));
    }

    [Theory]
    [InlineData("1", "01")]
    [InlineData("01", "1")]
    public void VersionsThatNormalizeToTheSameNumber_MakeThatVersionUnavailable(string first, string second)
    {
        var protector = Create("1", (first, KeyOne), (second, KeyTwo));

        Assert.False(protector.IsAvailable);
        Assert.Throws<CredentialProtectionUnavailableException>(() => Protect(protector));
    }

    [Fact]
    public void ADuplicatedVersion_DoesNotAffectOtherVersions()
    {
        var protector = Create("2", ("1", KeyOne), ("01", KeyTwo), ("2", KeyTwo));
        var old = Protect(Create("1", ("1", KeyOne)));

        Assert.True(protector.IsAvailable);
        Assert.False(protector.TryUnprotect(old, AssistantProvider.Anthropic, out _));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void MasterKeyOfAnotherLength_MakesThatVersionUnavailable(int bytes)
        => Assert.False(Create("1", ("1", Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)))).IsAvailable);

    [Fact]
    public void TheHighestVersion_IsUsableAndTheNextOneIsNot()
    {
        var protector = Create("32767", ("32767", KeyOne));

        Assert.True(protector.IsAvailable);
        Assert.Equal((short)32767, Protect(protector).KeyVersion);
        Assert.False(Create("32768", ("32768", KeyOne)).IsAvailable);
    }

    [Fact]
    public void AvailableProtector_HasNoUnavailableReason()
        => Assert.Equal(ProtectionUnavailableReason.None, Create("1", ("1", KeyOne)).UnavailableReason);

    [Theory]
    [InlineData(null, ProtectionUnavailableReason.NoActiveVersionConfigured)]
    [InlineData("", ProtectionUnavailableReason.NoActiveVersionConfigured)]
    [InlineData("2", ProtectionUnavailableReason.ActiveVersionWithoutKey)]
    [InlineData("abc", ProtectionUnavailableReason.ActiveVersionWithoutKey)]
    public void UnavailableReason_DistinguishesTheActiveVersionProblem(string? active, ProtectionUnavailableReason expected)
        => Assert.Equal(expected, Create(active, ("1", KeyOne)).UnavailableReason);

    [Fact]
    public void AnInvalidActiveKey_IsReportedAsInvalidOrDuplicate()
        => Assert.Equal(ProtectionUnavailableReason.InvalidOrDuplicateMasterKey, Create("1", ("1", "no-es-base64")).UnavailableReason);

    [Fact]
    public void ADuplicatedActiveVersion_IsReportedAsInvalidOrDuplicate()
        => Assert.Equal(ProtectionUnavailableReason.InvalidOrDuplicateMasterKey, Create("1", ("1", KeyOne), ("01", KeyTwo)).UnavailableReason);

    [Fact]
    public void WithoutMasterKeys_NothingIsAvailableOrDecryptable()
    {
        var withKey = Create("1", ("1", KeyOne));
        var stored = Protect(withKey);
        var without = Create(null);

        Assert.False(without.IsAvailable);
        Assert.False(without.TryUnprotect(stored, AssistantProvider.Anthropic, out var plaintext));
        Assert.Null(plaintext);
    }

    [Fact]
    public void Rotation_DecryptsOldVersionsAndEncryptsWithTheActiveOne()
    {
        var old = Protect(Create("1", ("1", KeyOne)));
        var rotated = Create("2", ("1", KeyOne), ("2", KeyTwo));

        Assert.True(rotated.TryUnprotect(old, AssistantProvider.Anthropic, out var plaintext));
        Assert.Equal(Secret, plaintext!.Value);
        Assert.Equal((short)2, Protect(rotated).KeyVersion);
    }

    [Fact]
    public void RetiredVersion_IsNotDecryptable()
    {
        var old = Protect(Create("1", ("1", KeyOne)));
        var retired = Create("2", ("2", KeyTwo));

        Assert.False(retired.TryUnprotect(old, AssistantProvider.Anthropic, out var plaintext));
        Assert.Null(plaintext);
    }
}
