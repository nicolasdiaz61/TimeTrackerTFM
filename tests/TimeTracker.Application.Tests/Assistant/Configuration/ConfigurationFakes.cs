using System.Diagnostics.CodeAnalysis;
using System.Text;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

internal sealed class InMemorySettingsStore : IAssistantSettingsStore
{
    private int _version = 100;

    public AssistantSettings? Row { get; set; }

    public List<AuditEntry> Audits { get; } = [];

    public int Writes { get; private set; }

    public Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Row);

    public Task<string> GetVersionAsync(CancellationToken cancellationToken) => Task.FromResult(Row?.Version ?? "0");

    public Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
    {
        if ((Row?.Version ?? "0") != expectedToken)
        {
            return Task.FromResult<SaveOutcome>(new SaveOutcome.Conflict());
        }

        Writes++;
        var token = (++_version).ToString();
        Row = next with { Version = token };
        Audits.Add(audit);
        return Task.FromResult<SaveOutcome>(new SaveOutcome.Saved(token));
    }

    public Task<bool> DeleteAsync(AuditEntry audit, CancellationToken cancellationToken)
    {
        if (Row is null)
        {
            return Task.FromResult(false);
        }

        Row = null;
        Writes++;
        Audits.Add(audit);
        return Task.FromResult(true);
    }
}

/// <summary>Cifrado de juguete reversible (XOR, sin texto legible): el texto cifrado lleva proveedor y versión para poder simular fallos de autenticación.</summary>
internal sealed class FakeProtector : IAssistantCredentialProtector
{
    public bool Available { get; set; } = true;

    public short ActiveVersion { get; set; } = 1;

    /// <summary>Simula que la clave maestra se pierde entre <c>IsAvailable</c> y <c>Protect</c>.</summary>
    public bool FailProtectAfterAvailabilityCheck { get; set; }

    public HashSet<short> RetiredVersions { get; } = [];

    public bool IsAvailable => Available;

    public ProtectionUnavailableReason UnavailableReason => Available ? ProtectionUnavailableReason.None : ProtectionUnavailableReason.NoActiveVersionConfigured;

    private static byte[] Scramble(byte[] data) => [.. data.Select(b => (byte)(b ^ 0x5A))];

    public EncryptedCredential Protect(AssistantCredential plaintext, AssistantProvider provider)
        => Available && !FailProtectAfterAvailabilityCheck
            ? new EncryptedCredential(Scramble(Encoding.UTF8.GetBytes($"{provider}|{plaintext.Value}")), new byte[12], ActiveVersion)
            : throw new CredentialProtectionUnavailableException();

    public bool TryUnprotect(EncryptedCredential stored, AssistantProvider provider, [NotNullWhen(true)] out AssistantCredential? plaintext)
    {
        plaintext = null;
        var text = Encoding.UTF8.GetString(Scramble(stored.Ciphertext));
        var prefix = $"{provider}|";
        if (RetiredVersions.Contains(stored.KeyVersion) || !text.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        plaintext = new AssistantCredential(text[prefix.Length..]);
        return true;
    }
}

internal sealed class FakeEnvironment(AssistantEnvironmentDefaults current) : IAssistantEnvironment
{
    public AssistantEnvironmentDefaults Current { get; } = current;
}

internal sealed class FakeReloader : IAssistantRuntimeReloader
{
    public int Calls { get; private set; }

    public bool Fails { get; set; }

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Fails ? Task.FromException(new InvalidOperationException("recarga fallida")) : Task.CompletedTask;
    }
}

internal sealed class FakeTester(ConnectionTestResult? result = null) : IAssistantConnectionTester
{
    public List<AssistantConnectionCandidate> Received { get; } = [];

    public Task<ConnectionTestResult> TestAsync(AssistantConnectionCandidate candidate, CancellationToken cancellationToken)
    {
        Received.Add(candidate);
        return Task.FromResult(result ?? new ConnectionTestResult("ok", "Conexión correcta."));
    }
}
