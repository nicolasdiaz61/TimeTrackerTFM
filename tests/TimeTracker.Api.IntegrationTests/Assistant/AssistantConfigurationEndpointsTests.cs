using System.Net;
using System.Text.Json;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>Los cuatro endpoints de <c>/api/asistente/configuracion</c> sobre HTTP real; la parte con base de datos usa PostgreSQL (Testcontainers).</summary>
[Collection("Postgres")]
public sealed class AssistantConfigurationEndpointsTests(PostgresFixture postgres)
{
    private const string ValidPut = """{"provider":"Ollama","model":"modelo-nuevo","timeoutSeconds":20,"maxOutputTokens":500,"effort":"low","concurrencyToken":"0"}""";

    public static TheoryData<string, string> AllEndpoints => new()
    {
        { "GET", Route },
        { "PUT", Route },
        { "DELETE", Route },
        { "POST", TestRoute },
    };

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static Task<long> CountAsync(string connectionString, string table, string where = "true")
        => ScalarAsync<long>(connectionString, $"SELECT count(*) FROM {table} WHERE {where}");

    private static HttpRequestMessage Request(string method, string route)
        => new(new HttpMethod(method), route) { Content = method is "PUT" or "POST" ? Json("{}") : null };

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Anonymous_Returns401_AndNothingIsReadOrWritten(string method, string route)
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateAnonymousClient().SendAsync(Request(method, route), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal((0, 0), (store.GetCalls, store.WriteCalls));
        Assert.Empty(factory.Tester.Candidates);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Employee_Returns403_AndNothingIsReadOrWritten(string method, string route)
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(role: "Employee", store: store);

        var response = await factory.CreateClient().SendAsync(Request(method, route), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal((0, 0), (store.GetCalls, store.WriteCalls));
        Assert.Empty(factory.Tester.Candidates);
    }

    [Fact]
    public async Task Get_AsManagerWithoutRow_Returns200WithTheEnvironmentAndTheDocumentedFields()
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateClient().GetAsync(Route, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(
            ["allowedProviders", "applied", "concurrencyToken", "credentialConfigured", "credentialStorageAvailable", "effort", "maxOutputTokens", "model", "provider", "sources", "timeoutSeconds", "updatedAt", "updatedBy"],
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("Ollama", AssistantConfigurationApiFactory.EnvironmentModel, "0", false, true, false), (
            root.GetProperty("provider").GetString(),
            root.GetProperty("model").GetString(),
            root.GetProperty("concurrencyToken").GetString(),
            root.GetProperty("credentialConfigured").GetBoolean(),
            root.GetProperty("credentialStorageAvailable").GetBoolean(),
            root.GetProperty("applied").GetBoolean()));
        Assert.Equal(
            ["env", "env", "env", "env", "env", "none"],
            new[] { "provider", "model", "timeoutSeconds", "maxOutputTokens", "effort", "credential" }
                .Select(name => root.GetProperty("sources").GetProperty(name).GetString()));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("updatedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("updatedBy").ValueKind);
        var allowed = root.GetProperty("allowedProviders").EnumerateArray().ToList();
        Assert.Equal(["Anthropic", "OpenAI", "Ollama"], allowed.Select(item => item.GetProperty("provider").GetString()));
        Assert.Equal([true, true, false], allowed.Select(item => item.GetProperty("requiresCredential").GetBoolean()));
        Assert.Equal((1, 0), (store.GetCalls, store.WriteCalls));
    }

    [Fact]
    public async Task Put_Valid_Returns200WithTheSameBodyAsGet_AuditsAndReloadsTheChat()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new AssistantConfigurationApiFactory(connectionString);
        var client = factory.CreateClient();

        var put = await client.PutJsonAsync(ValidPut);
        var get = await client.GetAsync(Route, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var putText = await put.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Equal(await get.Content.ReadAsStringAsync(CancellationToken.None), putText);
        using var body = JsonDocument.Parse(putText);
        var root = body.RootElement;
        Assert.Equal(("modelo-nuevo", 20, 500, "low", "stored", true), (
            root.GetProperty("model").GetString(),
            root.GetProperty("timeoutSeconds").GetInt32(),
            root.GetProperty("maxOutputTokens").GetInt32(),
            root.GetProperty("effort").GetString(),
            root.GetProperty("sources").GetProperty("model").GetString(),
            root.GetProperty("applied").GetBoolean()));
        Assert.NotEqual("0", root.GetProperty("concurrencyToken").GetString());
        Assert.Equal(1, await CountAsync(connectionString, "assistant_settings_audit", "action = 'Saved' AND credential_changed = false"));
        Assert.Equal("modelo-nuevo", factory.ChatFactory.Created[^1].Model);
    }

    [Fact]
    public async Task Get_ReportsTheDisplayNameOfWhoSaved_AndNullWhenTheUserNoLongerExists()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new AssistantConfigurationApiFactory(connectionString);
        var client = factory.CreateClient();
        await client.PutJsonAsync(ValidPut);
        using var withoutUser = await ReadJsonAsync(await client.GetAsync(Route, CancellationToken.None));
        await UserSeed.InsertRawAsync(connectionString, factory.ManagerId, $"gerente.{Guid.NewGuid():N}@example.test", "Manager");

        using var withUser = await ReadJsonAsync(await client.GetAsync(Route, CancellationToken.None));

        Assert.Equal(JsonValueKind.Null, withoutUser.RootElement.GetProperty("updatedBy").ValueKind);
        Assert.Equal("Usuario de prueba", withUser.RootElement.GetProperty("updatedBy").GetString());
        Assert.Equal(factory.ManagerId.ToString(), await ScalarAsync<string>(connectionString, "SELECT updated_by::text FROM assistant_settings"));
    }

    [Fact]
    public async Task Put_WithInvalidFields_Returns400PerFieldAndSavesNothing()
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateClient().PutJsonAsync(
            """{"provider":"Ollama","model":"<script>","timeoutSeconds":4,"effort":"extreme","concurrencyToken":"0"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await FieldErrorsAsync(response);
        Assert.Equal(["effort", "model", "timeoutSeconds"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.All(errors, pair => Assert.Contains(pair.Key, Assert.Single(pair.Value), StringComparison.Ordinal));
        Assert.Equal(0, store.WriteCalls);
    }

    [Fact]
    public async Task Put_WithoutConcurrencyToken_Returns400OnThatField()
    {
        using var factory = new AssistantConfigurationApiFactory(store: new CountingSettingsStore());

        var response = await factory.CreateClient().PutJsonAsync("""{"provider":"Ollama","model":"modelo-x"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("concurrencyToken", (await FieldErrorsAsync(response)).Keys);
    }

    [Fact]
    public async Task Put_ByTwoReaders_SecondGets409AndKeepsTheFirstSave()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new AssistantConfigurationApiFactory(connectionString);
        var client = factory.CreateClient();

        // Primera creación: dos lectores ven «sin fila» ("0"); el segundo pierde.
        var first = await client.PutJsonAsync(ValidPut);
        var second = await client.PutJsonAsync(ValidPut.Replace("modelo-nuevo", "modelo-otro", StringComparison.Ordinal));
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Conflict), (first.StatusCode, second.StatusCode));
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        // Actualización: el token leído antes de un guardado ajeno queda obsoleto.
        using var current = await ReadJsonAsync(await client.GetAsync(Route, CancellationToken.None));
        var token = current.RootElement.GetProperty("concurrencyToken").GetString();
        var winner = await client.PutJsonAsync($$"""{"provider":"Ollama","model":"modelo-ganador","concurrencyToken":"{{token}}"}""");
        var loser = await client.PutJsonAsync($$"""{"provider":"Ollama","model":"modelo-perdedor","concurrencyToken":"{{token}}"}""");
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Conflict), (winner.StatusCode, loser.StatusCode));
        using var final = await ReadJsonAsync(await client.GetAsync(Route, CancellationToken.None));
        Assert.Equal("modelo-ganador", final.RootElement.GetProperty("model").GetString());
        Assert.Equal(2, await CountAsync(connectionString, "assistant_settings_audit"));
    }

    [Fact]
    public async Task Delete_WithARow_Returns204_AuditsTheResetAndGetShowsTheEnvironment()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new AssistantConfigurationApiFactory(connectionString);
        var client = factory.CreateClient();
        await client.PutJsonAsync(ValidPut);

        var delete = await client.DeleteAsync(Route, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var body = await ReadJsonAsync(await client.GetAsync(Route, CancellationToken.None));
        Assert.Equal(("0", AssistantConfigurationApiFactory.EnvironmentModel, "env"), (
            body.RootElement.GetProperty("concurrencyToken").GetString(),
            body.RootElement.GetProperty("model").GetString(),
            body.RootElement.GetProperty("sources").GetProperty("model").GetString()));
        Assert.Equal(1, await CountAsync(connectionString, "assistant_settings_audit", "action = 'Reset'"));
    }

    [Fact]
    public async Task Delete_WithoutARow_Returns204_AndAuditsNothing()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new AssistantConfigurationApiFactory(connectionString);

        var delete = await factory.CreateClient().DeleteAsync(Route, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, await CountAsync(connectionString, "assistant_settings_audit"));
    }

    [Fact]
    public async Task Delete_WhenTheRowChangedAfterTheRead_Returns409()
    {
        using var factory = new AssistantConfigurationApiFactory(store: new RacingDeleteStore());

        var delete = await factory.CreateClient().DeleteAsync(Route, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal("application/problem+json", delete.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("POST")]
    public async Task BodyOver8KiB_Returns413_AndNothingIsReadOrWritten(string method)
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);
        factory.UseKestrel();
        var oversized = $$"""{"provider":"Ollama","model":"{{new string('a', 9 * 1024)}}","concurrencyToken":"0"}""";

        var response = await factory.CreateClient().SendAsync(
            new HttpRequestMessage(new HttpMethod(method), method == "PUT" ? Route : TestRoute) { Content = Json(oversized) }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal((0, 0), (store.GetCalls, store.WriteCalls));
        Assert.Empty(factory.Tester.Candidates);
    }
}
