using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>Cada prueba usa una base propia (la fila es única), migrada con las migraciones reales.</summary>
[Collection("Postgres")]
public sealed class AssistantSettingsStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Secret = "clave-prueba-123456";
    private const string NoRow = "0";

    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var context = postgres.CreateContext(_connectionString);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Save_WithTheNoRowToken_CreatesTheRowAndItsAuditAndReturnsTheNewToken()
    {
        var saved = await SaveAsync(Settings("m-1"), NoRow);

        var token = Assert.IsType<SaveOutcome.Saved>(saved).Token;
        Assert.NotEqual(NoRow, token);
        var row = await GetAsync();
        Assert.Equal(("m-1", token), (row!.Model, row.Version));
        Assert.Equal(1, await CountAsync("assistant_settings_audit"));
    }

    [Fact]
    public async Task Save_WithTheNoRowTokenWhenARowAlreadyExists_IsConflictAndChangesNothing()
    {
        await SaveAsync(Settings("first"), NoRow);

        var second = await SaveAsync(Settings("second"), NoRow);

        Assert.IsType<SaveOutcome.Conflict>(second);
        Assert.Equal("first", (await GetAsync())!.Model);
        Assert.Equal(1, await CountAsync("assistant_settings_audit"));
    }

    [Fact]
    public async Task Save_WithTheCurrentToken_UpdatesAndReturnsADifferentToken()
    {
        var first = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("first"), NoRow)).Token;

        var second = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("second"), first)).Token;

        Assert.NotEqual(first, second);
        var row = await GetAsync();
        Assert.Equal(("second", second), (row!.Model, row.Version));
        Assert.Equal(2, await CountAsync("assistant_settings_audit"));
    }

    [Fact]
    public async Task Save_WithAStaleToken_IsConflictAndChangesNothing()
    {
        var first = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("first"), NoRow)).Token;
        var second = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("second"), first)).Token;

        var stale = await SaveAsync(Settings("third"), first);

        Assert.IsType<SaveOutcome.Conflict>(stale);
        var row = await GetAsync();
        Assert.Equal(("second", second), (row!.Model, row.Version));
        Assert.Equal(2, await CountAsync("assistant_settings_audit"));
    }

    [Fact]
    public async Task Save_WithATokenAndNoRow_IsConflict()
    {
        Assert.IsType<SaveOutcome.Conflict>(await SaveAsync(Settings("m"), "12345"));

        Assert.Equal(0, await CountAsync("assistant_settings"));
    }

    [Fact]
    public async Task Save_WhenTheAuditInsertFails_LeavesNoRow()
    {
        await ExecuteAsync("DROP TABLE assistant_settings_audit");

        await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(Settings("m"), NoRow));

        Assert.Equal(0, await CountAsync("assistant_settings"));
    }

    [Fact]
    public async Task Save_WhenTheAuditInsertFailsOnAnUpdate_KeepsThePreviousRow()
    {
        var first = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("first"), NoRow)).Token;
        await ExecuteAsync("DROP TABLE assistant_settings_audit");

        await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(Settings("second"), first));

        var row = await GetAsync();
        Assert.Equal(("first", first), (row!.Model, row.Version));
    }

    [Fact]
    public async Task Save_ThenGet_RoundTripsEveryField()
    {
        var credential = new EncryptedCredential([1, 2, 3, 4], new byte[12], 7);
        var settings = new AssistantSettings(AssistantProvider.OpenAI, "gpt-x", 25, 800, AssistantEffort.High, credential, Ana, Now, NoRow);

        var token = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(settings, NoRow)).Token;

        var row = (await GetAsync())!;
        Assert.Equal(settings with { Credential = null, Version = token }, row with { Credential = null });
        Assert.Equal([1, 2, 3, 4], row.Credential!.Ciphertext);
        Assert.Equal(new byte[12], row.Credential.Nonce);
        Assert.Equal((short)7, row.Credential.KeyVersion);
    }

    [Fact]
    public async Task Save_ThenGet_RoundTripsAllOptionalFieldsAsNull()
    {
        var settings = new AssistantSettings(AssistantProvider.None, null, null, null, null, null, Ana, Now, NoRow);

        await SaveAsync(settings, NoRow);

        var row = (await GetAsync())!;
        Assert.Equal((AssistantProvider.None, null, null, null, null, null), (row.Provider, row.Model, row.TimeoutSeconds, row.MaxOutputTokens, row.Effort, row.Credential));
    }

    [Fact]
    public async Task TheStoredCredential_DoesNotContainThePlainKeyAndDecryptsBack()
    {
        var protector = CreateProtector();
        var encrypted = protector.Protect(new AssistantCredential(Secret), AssistantProvider.Anthropic, Company.DefaultId);

        await SaveAsync(Settings("m", encrypted), NoRow);

        var column = (byte[])(await ScalarAsync("SELECT credential_ciphertext FROM assistant_settings"))!;
        Assert.DoesNotContain(Secret, Encoding.UTF8.GetString(column), StringComparison.Ordinal);
        var stored = (await GetAsync())!.Credential!;
        Assert.True(protector.TryUnprotect(stored, AssistantProvider.Anthropic, Company.DefaultId, out var plaintext));
        Assert.Equal(Secret, plaintext!.Value);
    }

    [Theory]
    [InlineData("INSERT INTO assistant_settings (company_id, provider, updated_by, updated_at) VALUES (gen_random_uuid(), 'None', gen_random_uuid(), now())", "fk_assistant_settings_companies_company_id")]
    [InlineData("INSERT INTO assistant_settings (company_id, provider, updated_by, updated_at, credential_ciphertext) VALUES ('00000000-0000-7000-8000-000000000001', 'None', gen_random_uuid(), now(), '\\x01')", "ck_assistant_settings_credential_all_or_none")]
    [InlineData("INSERT INTO assistant_settings (company_id, provider, updated_by, updated_at, credential_ciphertext, credential_nonce, credential_key_version) VALUES ('00000000-0000-7000-8000-000000000001', 'None', gen_random_uuid(), now(), '\\x01', '\\x0102', 1)", "ck_assistant_settings_credential_nonce_length")]
    public async Task Check_RejectsInvalidRows(string sql, string constraint)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql));

        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task Check_AcceptsARowWithTheThreeCredentialColumnsAndAValidNonce()
        => await ExecuteAsync("INSERT INTO assistant_settings (company_id, provider, updated_by, updated_at, credential_ciphertext, credential_nonce, credential_key_version) VALUES ('00000000-0000-7000-8000-000000000001', 'None', gen_random_uuid(), now(), '\\x01', '\\x000102030405060708090a0b', 1)");

    [Fact]
    public async Task Delete_WithTheCurrentToken_RemovesTheRowAndAuditsTheReset()
    {
        var token = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("m"), NoRow)).Token;

        var outcome = await DeleteAsync(token);

        Assert.Equal(DeleteOutcome.Deleted, outcome);
        Assert.Equal(0, await CountAsync("assistant_settings"));
        Assert.Equal(["Saved", "Reset"], await ColumnAsync("SELECT action FROM assistant_settings_audit ORDER BY id"));
    }

    [Fact]
    public async Task Delete_WithoutARow_IsNotFoundAndAuditsNothing()
    {
        Assert.Equal(DeleteOutcome.NotFound, await DeleteAsync(NoRow));

        Assert.Equal(0, await CountAsync("assistant_settings_audit"));
    }

    [Fact]
    public async Task Delete_WithAStaleToken_IsConflictAndKeepsTheRowWithoutAuditing()
    {
        var first = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("first"), NoRow)).Token;
        var second = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("second"), first)).Token;

        Assert.Equal(DeleteOutcome.Conflict, await DeleteAsync(first));

        Assert.Equal(second, (await GetAsync())!.Version);
        Assert.Equal(2, await CountAsync("assistant_settings_audit"));
    }

    [Fact]
    public async Task Delete_WhenTheRowWasAlreadyDeletedAfterTheRead_IsNotFoundAndAuditsNothing()
    {
        var token = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("m"), NoRow)).Token;
        var store = RacingStore("DELETE FROM assistant_settings");

        var outcome = await store.DeleteAsync(token, Audit(AuditAction.Reset), CancellationToken.None);

        Assert.Equal(DeleteOutcome.NotFound, outcome);
        Assert.Equal(["Saved"], await ColumnAsync("SELECT action FROM assistant_settings_audit ORDER BY id"));
    }

    [Fact]
    public async Task Delete_WhenAnotherManagerSavedAfterTheRead_IsConflictAndKeepsTheirRow()
    {
        var token = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("m"), NoRow)).Token;
        var store = RacingStore("UPDATE assistant_settings SET model = 'otro'");

        var outcome = await store.DeleteAsync(token, Audit(AuditAction.Reset), CancellationToken.None);

        Assert.Equal(DeleteOutcome.Conflict, outcome);
        Assert.Equal("otro", (await GetAsync())!.Model);
        Assert.Equal(1, await CountAsync("assistant_settings_audit"));
    }

    [Fact]
    public async Task GetVersion_IsZeroWithoutARowAndTheTokenAfterSaving()
    {
        Assert.Equal(NoRow, await NewStore().GetVersionAsync(CancellationToken.None));

        var token = Assert.IsType<SaveOutcome.Saved>(await SaveAsync(Settings("m"), NoRow)).Token;

        Assert.Equal(token, await NewStore().GetVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WhenTheTableDoesNotExist_ReadsReturnNoRowAndZero()
    {
        var emptyDatabase = await postgres.CreateEmptyDatabaseAsync();
        var store = new AssistantSettingsStore(postgres.CreateContext(emptyDatabase));

        Assert.Null(await store.GetAsync(CancellationToken.None));
        Assert.Equal(NoRow, await store.GetVersionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WhenTheDatabaseIsUnreachable_TheExceptionPropagates()
    {
        var unreachable = new NpgsqlConnectionStringBuilder(_connectionString) { Port = 1, Timeout = 2 }.ConnectionString;
        var store = new AssistantSettingsStore(postgres.CreateContext(unreachable));

        // EF Core envuelve el fallo de conexión: lo que importa es que no se confunde con «sin fila».
        var read = await Assert.ThrowsAnyAsync<Exception>(() => store.GetAsync(CancellationToken.None));
        var version = await Assert.ThrowsAnyAsync<Exception>(() => store.GetVersionAsync(CancellationToken.None));
        Assert.All([read, version], exception => Assert.Contains(Chain(exception), inner => inner is NpgsqlException));
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            yield return current;
        }
    }

    private static AssistantSettings Settings(string model, EncryptedCredential? credential = null)
        => new(AssistantProvider.Anthropic, model, 25, 800, AssistantEffort.Low, credential, Ana, Now, NoRow);

    private static AuditEntry Audit(AuditAction action = AuditAction.Saved)
        => new(Ana, Now, action, CredentialChanged: false, Previous: null, Current: null);

    private static AesGcmCredentialProtector CreateProtector()
    {
        var options = new AiOptions { ActiveMasterKeyVersion = "1" };
        options.MasterKeys["1"] = Convert.ToBase64String(new byte[32]);
        return new AesGcmCredentialProtector(Options.Create(options));
    }

    private AssistantSettingsStore RacingStore(string sql)
        => new(new TimeTrackerDbContext(new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(new RacingSqlInterceptor(_connectionString, sql))
            .Options, new TestCompanyContext(Company.DefaultId)));

    private AssistantSettingsStore NewStore() => new(postgres.CreateContext(_connectionString));

    private Task<SaveOutcome> SaveAsync(AssistantSettings settings, string token)
        => NewStore().SaveAsync(settings, token, Audit(), CancellationToken.None);

    private Task<DeleteOutcome> DeleteAsync(string token)
        => NewStore().DeleteAsync(token, Audit(AuditAction.Reset), CancellationToken.None);

    private Task<AssistantSettings?> GetAsync() => NewStore().GetAsync(CancellationToken.None);

    private async Task<int> CountAsync(string table) => Convert.ToInt32(await ScalarAsync($"SELECT count(*) FROM {table}"), System.Globalization.CultureInfo.InvariantCulture);

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(CancellationToken.None);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private async Task<List<string>> ColumnAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var rows = new List<string>();
        while (await reader.ReadAsync(CancellationToken.None))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
