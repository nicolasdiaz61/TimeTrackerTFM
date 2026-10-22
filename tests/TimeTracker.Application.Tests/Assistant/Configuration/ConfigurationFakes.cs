using System.Diagnostics.CodeAnalysis;
using System.Text;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

internal sealed class InMemorySettingsStore : IAssistantSettingsStore
{
    private int _version = 100;

    public AssistantSettings? Row { get; set; }

    public List<AuditEntry> Audits { get; } = [];

    public int Writes { get; private set; }

    /// <summary>Simula a otro Manager que cambia la fila justo después de leerla.</summary>
    public Action<InMemorySettingsStore>? AfterRead { get; set; }

    public Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken)
    {
        var read = Row;
        AfterRead?.Invoke(this);
        return Task.FromResult(read);
    }

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

    public Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
    {
        if (Row is null)
        {
            return Task.FromResult(DeleteOutcome.NotFound);
        }

        if (Row.Version != expectedToken)
        {
            return Task.FromResult(DeleteOutcome.Conflict);
        }

        Row = null;
        Writes++;
        Audits.Add(audit);
        return Task.FromResult(DeleteOutcome.Deleted);
    }

    public Task<IReadOnlyList<CompanyVersion>> ListVersionsAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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

    /// <summary>Texto cifrado v2: vinculado a la compañía y al proveedor, como el protector real.</summary>
    public EncryptedCredential Protect(AssistantCredential plaintext, AssistantProvider provider, Guid companyId)
        => Available && !FailProtectAfterAvailabilityCheck
            ? new EncryptedCredential(Scramble(Encoding.UTF8.GetBytes($"{companyId:D}|{provider}|{plaintext.Value}")), new byte[12], ActiveVersion)
            : throw new CredentialProtectionUnavailableException();

    /// <summary>Texto cifrado v1 anterior a este cambio (sin compañía): solo se abre en la compañía por defecto, como el protector real.</summary>
    public EncryptedCredential ProtectAsLegacyV1(AssistantCredential plaintext, AssistantProvider provider)
        => new(Scramble(Encoding.UTF8.GetBytes($"{provider}|{plaintext.Value}")), new byte[12], ActiveVersion);

    public bool TryUnprotect(EncryptedCredential stored, AssistantProvider provider, Guid companyId, [NotNullWhen(true)] out AssistantCredential? plaintext)
    {
        plaintext = null;
        var text = Encoding.UTF8.GetString(Scramble(stored.Ciphertext));
        var v2Prefix = $"{companyId:D}|{provider}|";
        var v1Prefix = $"{provider}|";
        if (RetiredVersions.Contains(stored.KeyVersion))
        {
            return false;
        }

        if (text.StartsWith(v2Prefix, StringComparison.Ordinal))
        {
            plaintext = new AssistantCredential(text[v2Prefix.Length..]);
        }
        else if (companyId == Company.DefaultId && text.StartsWith(v1Prefix, StringComparison.Ordinal))
        {
            plaintext = new AssistantCredential(text[v1Prefix.Length..]);
        }

        return plaintext is not null;
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

    public List<Guid> Companies { get; } = [];

    public Task ReloadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        Calls++;
        Companies.Add(companyId);
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
