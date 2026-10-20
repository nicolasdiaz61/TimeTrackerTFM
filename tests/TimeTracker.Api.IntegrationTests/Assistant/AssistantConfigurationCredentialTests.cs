using System.Net;
using System.Text.Json;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>Protección de la clave (CP) a través de los endpoints: fallo cerrado sin clave maestra y clave de solo escritura.</summary>
[Collection("Postgres")]
public sealed class AssistantConfigurationCredentialTests(PostgresFixture postgres)
{
    private const string PutWithKey = $$"""{"provider":"Anthropic","model":"modelo-x","credential":"{{Key}}","concurrencyToken":"0"}""";

    private static async Task<long> CountAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<byte[]> StoredCiphertextAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT credential_ciphertext FROM assistant_settings", connection);
        return (byte[])(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Put_WithAKeyAndNoMasterKey_Returns400OnCredentialAndWritesNothing()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new AssistantConfigurationApiFactory(connectionString, masterKeyAvailable: false);

        var response = await factory.CreateClient().PutJsonAsync(PutWithKey);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Equal(["credential"], (await FieldErrorsAsync(response)).Keys);
        Assert.DoesNotContain(Key, text, StringComparison.Ordinal);
        Assert.Equal((0, 0), (await CountAsync(connectionString, "assistant_settings"), await CountAsync(connectionString, "assistant_settings_audit")));
    }

    [Fact]
    public async Task Get_WithoutMasterKey_ReportsStorageUnavailable_AndWithItReportsAvailable()
    {
        using var without = new AssistantConfigurationApiFactory(store: new CountingSettingsStore(), masterKeyAvailable: false);
        using var with = new AssistantConfigurationApiFactory(store: new CountingSettingsStore());

        using var withoutBody = await ReadJsonAsync(await without.CreateClient().GetAsync(Route, CancellationToken.None));
        using var withBody = await ReadJsonAsync(await with.CreateClient().GetAsync(Route, CancellationToken.None));

        Assert.False(withoutBody.RootElement.GetProperty("credentialStorageAvailable").GetBoolean());
        Assert.True(withBody.RootElement.GetProperty("credentialStorageAvailable").GetBoolean());
    }

    [Fact]
    public async Task Put_WithProviderNoneAndAKey_Returns400OnCredential()
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateClient().PutJsonAsync($$"""{"provider":"None","credential":"{{Key}}","concurrencyToken":"0"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("credential", (await FieldErrorsAsync(response)).Keys);
        Assert.Equal(0, store.WriteCalls);
    }

    [Fact]
    public async Task TheKeyIsWriteOnly_NoResponseCarriesItInPlainOrEncryptedForm_AndTheIndicatorDropsAfterDelete()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new AssistantConfigurationApiFactory(connectionString);
        var client = factory.CreateClient();

        var put = await client.PutJsonAsync(PutWithKey);
        var get = await client.GetAsync(Route, CancellationToken.None);
        var test = await client.PostTestAsync($$"""{"credential":"{{Key}}"}""");

        var ciphertext = await StoredCiphertextAsync(connectionString);
        Assert.NotEmpty(ciphertext);
        var forbidden = new[] { Key, Convert.ToBase64String(ciphertext), Convert.ToHexString(ciphertext), Convert.ToHexStringLower(ciphertext), AssistantConfigurationApiFactory.MasterKey };
        foreach (var response in new[] { put, get, test })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
            Assert.All(forbidden, secret => Assert.DoesNotContain(secret, text, StringComparison.Ordinal));
            Assert.DoesNotContain("credential", JsonDocument.Parse(text).RootElement.EnumerateObject().Select(property => property.Name));
        }

        using var stored = await ReadJsonAsync(get);
        Assert.Equal((true, "stored"), (stored.RootElement.GetProperty("credentialConfigured").GetBoolean(), stored.RootElement.GetProperty("sources").GetProperty("credential").GetString()));
        Assert.Equal(Key, factory.Tester.Candidates.Single().Credential!.Value);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(Route, CancellationToken.None)).StatusCode);
        using var afterDelete = await ReadJsonAsync(await client.GetAsync(Route, CancellationToken.None));
        Assert.Equal((false, "none"), (afterDelete.RootElement.GetProperty("credentialConfigured").GetBoolean(), afterDelete.RootElement.GetProperty("sources").GetProperty("credential").GetString()));
    }

    [Fact]
    public async Task AnInvalidResponse400_NeverEchoesTheCandidateKey()
    {
        using var factory = new AssistantConfigurationApiFactory(store: new CountingSettingsStore());

        var response = await factory.CreateClient().PutJsonAsync($$"""{"provider":"Anthropic","model":"<script>","credential":"{{Key}}","concurrencyToken":"0"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(Key, await response.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Contains("model", body.RootElement.GetProperty("errors").EnumerateObject().Select(property => property.Name));
    }
}
