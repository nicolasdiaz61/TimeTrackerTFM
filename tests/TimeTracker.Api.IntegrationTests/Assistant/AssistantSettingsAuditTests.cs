using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>Lo que llega a la tabla de auditoría: solo valores no secretos y el indicador de cambio de clave.</summary>
[Collection("Postgres")]
public sealed class AssistantSettingsAuditTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string NoRow = "0";

    private static readonly string[] ExpectedKeys = ["credentialConfigured", "effort", "maxOutputTokens", "model", "provider", "timeoutSeconds"];
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    // Texto reconocible dentro del «cifrado» para poder buscarlo en la auditoría.
    private static readonly byte[] Ciphertext = [.. "clave-prueba-123456"u8, 0xAB, 0xCD];

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var context = postgres.CreateContext(_connectionString);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ASaveWithANewKey_RecordsOnlyNonSecretValuesAndFlagsTheKeyChange()
    {
        var next = Settings("m-1", credential: new EncryptedCredential(Ciphertext, new byte[12], 1));

        await SaveAsync(next, NoRow, previous: null, credentialChanged: true);

        var audit = Assert.Single(await ReadAuditAsync());
        Assert.Equal(("Saved", true, Ana, Now), (audit.Action, audit.CredentialChanged, audit.ActorId, audit.OccurredAt));
        Assert.Null(audit.Previous);
        Assert.Equal(ExpectedKeys, JsonKeys(audit.Current!));
        using var current = JsonDocument.Parse(audit.Current!);
        Assert.Equal(("Anthropic", "m-1", 25, 800, "low", true), (
            current.RootElement.GetProperty("provider").GetString(),
            current.RootElement.GetProperty("model").GetString(),
            current.RootElement.GetProperty("timeoutSeconds").GetInt32(),
            current.RootElement.GetProperty("maxOutputTokens").GetInt32(),
            current.RootElement.GetProperty("effort").GetString(),
            current.RootElement.GetProperty("credentialConfigured").GetBoolean()));
        Assert.DoesNotContain(await AuditRowsAsText(), text => ContainsAnyFormOfTheKey(text));
    }

    [Fact]
    public async Task AnUpdateKeepingTheKey_RecordsThePreviousAndCurrentValuesWithoutFlaggingAKeyChange()
    {
        var first = Settings("old-m", credential: new EncryptedCredential(Ciphertext, new byte[12], 1));
        var token = await SaveAsync(first, NoRow, previous: null, credentialChanged: true);

        await SaveAsync(Settings("new-m", credential: first.Credential), token, previous: first, credentialChanged: false);

        var update = (await ReadAuditAsync())[1];
        Assert.False(update.CredentialChanged);
        Assert.Equal(ExpectedKeys, JsonKeys(update.Previous!));
        Assert.Equal(ExpectedKeys, JsonKeys(update.Current!));
        Assert.Equal(("old-m", "new-m"), (Model(update.Previous!), Model(update.Current!)));
        Assert.DoesNotContain(await AuditRowsAsText(), text => ContainsAnyFormOfTheKey(text));
    }

    [Fact]
    public async Task AReset_RecordsThePreviousValuesAndNoCurrentOnes()
    {
        var stored = Settings("db-m", credential: new EncryptedCredential(Ciphertext, new byte[12], 1));
        var token = await SaveAsync(stored, NoRow, previous: null, credentialChanged: true);
        var reset = new AuditEntry(Ana, Now, AuditAction.Reset, CredentialChanged: true, AuditSnapshot.From(stored), Current: null);

        await new AssistantSettingsStore(postgres.CreateContext(_connectionString)).DeleteAsync(token, reset, CancellationToken.None);

        var audit = (await ReadAuditAsync())[1];
        Assert.Equal(("Reset", true), (audit.Action, audit.CredentialChanged));
        Assert.Equal("db-m", Model(audit.Previous!));
        Assert.Null(audit.Current);
        Assert.DoesNotContain(await AuditRowsAsText(), text => ContainsAnyFormOfTheKey(text));
    }

    private static AssistantSettings Settings(string model, EncryptedCredential? credential)
        => new(AssistantProvider.Anthropic, model, 25, 800, AssistantEffort.Low, credential, Ana, Now, NoRow);

    private static bool ContainsAnyFormOfTheKey(string text)
        => text.Contains("clave-prueba-123456", StringComparison.Ordinal)
            || text.Contains(Convert.ToBase64String(Ciphertext), StringComparison.Ordinal)
            || text.Contains(Convert.ToHexString(Ciphertext), StringComparison.OrdinalIgnoreCase);

    private static string[] JsonKeys(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
    }

    private static string Model(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("model").GetString()!;
    }

    private async Task<string> SaveAsync(AssistantSettings next, string token, AssistantSettings? previous, bool credentialChanged)
    {
        var audit = new AuditEntry(Ana, Now, AuditAction.Saved, credentialChanged, AuditSnapshot.From(previous), AuditSnapshot.From(next));
        var outcome = await new AssistantSettingsStore(postgres.CreateContext(_connectionString)).SaveAsync(next, token, audit, CancellationToken.None);
        return Assert.IsType<SaveOutcome.Saved>(outcome).Token;
    }

    private async Task<List<AuditRow>> ReadAuditAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand("SELECT action, credential_changed, actor_id, occurred_at, previous::text, current::text FROM assistant_settings_audit ORDER BY id", connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var rows = new List<AuditRow>();
        while (await reader.ReadAsync(CancellationToken.None))
        {
            rows.Add(new AuditRow(
                reader.GetString(0),
                reader.GetBoolean(1),
                reader.GetGuid(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return rows;
    }

    private async Task<List<string>> AuditRowsAsText()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand("SELECT a::text FROM assistant_settings_audit a", connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var rows = new List<string>();
        while (await reader.ReadAsync(CancellationToken.None))
        {
            rows.Add(reader.GetString(0));
        }

        Assert.NotEmpty(rows);
        return rows;
    }

    private sealed record AuditRow(string Action, bool CredentialChanged, Guid ActorId, DateTimeOffset OccurredAt, string? Previous, string? Current);
}
