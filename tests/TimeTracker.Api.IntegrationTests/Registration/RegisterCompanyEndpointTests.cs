using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Identity;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Assistant;
using TimeTracker.Api.IntegrationTests.Authentication;
using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Support.RegistrationApiFactory;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>
/// <c>POST /api/auth/register-company</c> con el registro abierto (company-registration): código en tiempo constante antes que nada,
/// creación atómica, validación por campo, unicidad, privacidad del registro y compañía nueva sin asistente. Valores inventados.
/// </summary>
[Collection("Postgres")]
public class RegisterCompanyEndpointTests(PostgresFixture postgres)
{
    private const string GenericCodeDetail = "El código de invitación no es válido.";

    private static readonly string MasterKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    [Fact]
    public async Task ValidRegistration_Returns201WithTheLoginBodyAndTheTokenOfTheNewCompany()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var body = Body(companyName: "  Acme   Norte " + Guid.NewGuid().ToString("N"), email: $"Ana.{Guid.NewGuid():N}@Example.test");

        var response = await PostAsync(factory.CreateAnonymousClient(), body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = await ReadAsync(response);
        var user = json.RootElement.GetProperty("user");
        var email = ((string)body["email"]!).ToLowerInvariant();
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Ana Gómez", user.GetProperty("displayName").GetString());
        Assert.Equal("Manager", user.GetProperty("role").GetString());
        Assert.False(user.GetProperty("mustChangePassword").GetBoolean());
        Assert.StartsWith("Acme   Norte", user.GetProperty("companyName").GetString(), StringComparison.Ordinal);
        Assert.False(json.RootElement.TryGetProperty("companyId", out _));
        Assert.False(user.TryGetProperty("companyId", out _));
        Assert.EndsWith("Z", json.RootElement.GetProperty("expiresAt").GetString(), StringComparison.Ordinal);

        var companyId = (await ScalarAsync($"SELECT company_id::text FROM users WHERE email = '{email}'"))!;
        var payload = TokenPayload(json.RootElement.GetProperty("accessToken").GetString()!);
        Assert.Equal(companyId, payload.GetProperty("cid").GetString());
        Assert.Equal(user.GetProperty("companyName").GetString(), payload.GetProperty("cname").GetString());
        Assert.False(payload.TryGetProperty("mcp", out _));
        Assert.Equal(["Manager|true|false"], await QueryAsync($"SELECT role || '|' || (manager_id IS NULL) || '|' || must_change_password FROM users WHERE company_id = '{companyId}'"));
        Assert.Equal(["Active|false"], await QueryAsync($"SELECT status || '|' || uses_platform_assistant FROM companies WHERE id = '{companyId}'"));
    }

    [Fact]
    public async Task TheNewPersonIsInside_TheTokenOfTheResponseWorksOnAuthenticatedEndpoints()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        var body = Body();

        using var json = await ReadAsync(await PostAsync(client, body));
        client.DefaultRequestHeaders.Authorization = new("Bearer", json.RootElement.GetProperty("accessToken").GetString());

        var me = await client.GetAsync("/api/auth/me", CancellationToken.None);
        var team = await client.GetAsync("/api/team/members", CancellationToken.None);
        var entries = await client.GetAsync("/api/time-entries", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.OK, team.StatusCode);
        Assert.Equal(HttpStatusCode.OK, entries.StatusCode);
        using var teamJson = await ReadAsync(team);
        Assert.Empty(teamJson.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ThePassword_IsStoredOnlyAsAHashThatVerifiesWithItsOuterSpaces()
    {
        const string WithSpaces = "  contrasena con espacios  ";
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var body = Body(password: WithSpaces);
        var email = (string)body["email"]!;

        Assert.Equal(HttpStatusCode.Created, (await PostAsync(factory.CreateAnonymousClient(), body)).StatusCode);

        var hash = (await ScalarAsync($"SELECT password_hash FROM users WHERE email = '{email}'"))!;
        Assert.DoesNotContain(WithSpaces.Trim(), hash, StringComparison.Ordinal);
        var verifier = new PasswordHasher<object>();
        Assert.NotEqual(PasswordVerificationResult.Failed, verifier.VerifyHashedPassword(new object(), hash, WithSpaces));
        Assert.Equal(PasswordVerificationResult.Failed, verifier.VerifyHashedPassword(new object(), hash, WithSpaces.Trim()));
        // Y el inicio de sesión real lo confirma: entra con los espacios y no sin ellos.
        Assert.Equal(HttpStatusCode.OK, (await LoginEndpointTests.PostLoginAsync(factory.CreateAnonymousClient(), email, WithSpaces)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginEndpointTests.PostLoginAsync(factory.CreateAnonymousClient(), email, WithSpaces.Trim())).StatusCode);
    }

    [Fact]
    public async Task EachConfiguredCode_IsAccepted_AndNeitherIsConsumed()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();

        foreach (var code in new[] { Code, SecondCode, Code, SecondCode })
        {
            Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, Body(inviteCode: code))).StatusCode);
        }
    }

    [Fact]
    public async Task AnAbsentBlankOrWrongCode_ReturnsTheSame403_AndCreatesNothing()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        var absent = Body();
        absent.Remove("inviteCode");
        var before = await ScalarAsync("SELECT count(*)::text FROM companies");

        var responses = new[]
        {
            await PostAsync(client, absent),
            await PostAsync(client, Body(inviteCode: "   ")),
            await PostAsync(client, Body(inviteCode: "codigo-incorrecto-0123456789")),
            await PostAsync(client, Body(inviteCode: new string('x', 200))),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        var bodies = new List<string>();
        foreach (var response in responses)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            bodies.Add(WithoutTraceId(await response.Content.ReadAsStringAsync(CancellationToken.None)));
        }

        Assert.Single(bodies.Distinct());
        Assert.Contains(GenericCodeDetail, bodies[0], StringComparison.Ordinal);
        Assert.Equal(before, await ScalarAsync("SELECT count(*)::text FROM companies"));
    }

    [Fact]
    public async Task AWrongCodeWithAnAlreadyRegisteredEmail_Returns403NotTheConflict()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        var first = Body();
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, first)).StatusCode);
        var second = Body(email: (string)first["email"]!, inviteCode: "codigo-incorrecto-0123456789");

        var response = await PostAsync(client, second);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ARepeatedNormalizedName_Returns409WithItsOwnDetail_AndCreatesNothing()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        var token = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, Body(companyName: $"Acme Norte {token}"))).StatusCode);

        var response = await PostAsync(client, Body(companyName: $"  acme   NORTE   {token.ToUpperInvariant()}  "));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = await ReadAsync(response);
        Assert.Equal("Ya existe una compañía con ese nombre.", json.RootElement.GetProperty("detail").GetString());
        Assert.Equal(["1"], await QueryAsync($"SELECT count(*)::text FROM companies WHERE name_normalized = 'acme norte {token}'"));
    }

    [Fact]
    public async Task ANameThatDiffersOnlyByTheAccent_Returns201()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        var token = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, Body(companyName: $"Acme Norte {token}"))).StatusCode);

        var response = await PostAsync(client, Body(companyName: $"Ácme Norte {token}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ARepeatedEmail_Returns409WithAGenericDetailThatNamesNoCompanyOrRole_AndCreatesNoCompany()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        var first = Body(companyName: $"Acme Norte {Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, first)).StatusCode);
        var secondName = $"Segunda {Guid.NewGuid():N}";

        var response = await PostAsync(client, Body(companyName: secondName, email: (string)first["email"]!));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = await ReadAsync(response);
        var detail = json.RootElement.GetProperty("detail").GetString()!;
        Assert.Equal("Ya existe una cuenta con ese correo electrónico.", detail);
        Assert.DoesNotContain("Acme", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Manager", detail, StringComparison.Ordinal);
        Assert.Equal(["0"], await QueryAsync($"SELECT count(*)::text FROM companies WHERE name = '{secondName}'"));
    }

    [Fact]
    public async Task TwoSimultaneousIdenticalRegistrations_YieldExactlyOneCreatedAndOneConflict()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var body = Body();

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => PostAsync(factory.CreateAnonymousClient(), body)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(["1"], await QueryAsync($"SELECT count(*)::text FROM companies WHERE name = '{(string)body["companyName"]!}'"));
    }

    public static TheoryData<string, string?, string> InvalidFields => new()
    {
        { "companyName", " ", "companyName" },
        { "companyName", new string('c', 101), "companyName" },
        { "managerName", " ", "managerName" },
        { "managerName", new string('m', 101), "managerName" },
        { "email", "sin-arroba", "email" },
        { "email", " ", "email" },
        { "password", "corta-123", "password" },
        { "password", null, "password" },
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task AnInvalidField_Returns400NamingItOnly_WithoutEchoingThePasswordOrTheCode(string field, string? value, string expected)
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var body = Body();
        body[field] = value;
        var before = await ScalarAsync("SELECT count(*)::text FROM companies");

        var response = await PostAsync(factory.CreateAnonymousClient(), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var json = JsonDocument.Parse(text);
        Assert.Equal([expected], json.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.DoesNotContain("corta-123", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Code, text, StringComparison.Ordinal);
        Assert.Equal(before, await ScalarAsync("SELECT count(*)::text FROM companies"));
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task ThePasswordLimits_AreInclusive(int length, bool accepted)
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);

        var response = await PostAsync(factory.CreateAnonymousClient(), Body(password: new string('p', length)));

        Assert.Equal(accepted ? HttpStatusCode.Created : HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"companyName\": ")]
    [InlineData("esto no es json")]
    public async Task AnAbsentOrUnreadableBody_Returns400(string? json)
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);

        var response = await PostRawAsync(factory.CreateAnonymousClient(), json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownProperties_AreIgnored_TheCompanyIsNewWithoutPlatformAssistantAndThePersonIsAManager()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var body = Body();
        body["role"] = "Employee";
        body["companyId"] = "00000000-0000-7000-8000-000000000001";
        body["usesPlatformAssistant"] = true;
        body["mustChangePassword"] = true;
        var email = (string)body["email"]!;

        var response = await PostAsync(factory.CreateAnonymousClient(), body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["Manager|false|false"], await QueryAsync($"SELECT role || '|' || must_change_password || '|' || (SELECT uses_platform_assistant FROM companies WHERE id = company_id) FROM users WHERE email = '{email}'"));
        Assert.Equal(["1"], await QueryAsync($"SELECT count(*)::text FROM users WHERE email = '{email}' AND company_id <> '00000000-0000-7000-8000-000000000001'"));
    }

    [Fact]
    public async Task ABodyOfMoreThan4KiB_Returns413WithoutCreatingAnything()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        factory.UseKestrel();
        var body = Body();
        body["padding"] = new string('a', 5_000);
        var before = await ScalarAsync("SELECT count(*)::text FROM companies");

        var response = await PostAsync(factory.CreateAnonymousClient(), body);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(before, await ScalarAsync("SELECT count(*)::text FROM companies"));
    }

    [Fact]
    public async Task ARegisteredCompany_HasTheAssistantDisabled_EvenWithAPlatformProviderInTheEnvironment()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new ReloadApiFactory(connectionString, applyMigrations: true, masterKey: MasterKey, settings: new Dictionary<string, string?>
        {
            ["Registration:Enabled"] = "true",
            ["Registration:InviteCodes"] = Code,
        });
        var client = factory.CreateAnonymousClient();
        var createdBefore = factory.ChatFactory.Created.Count;

        using var json = await ReadAsync(await PostAsync(client, Body()));
        client.DefaultRequestHeaders.Authorization = new("Bearer", json.RootElement.GetProperty("accessToken").GetString());
        var chat = await client.AskAsync("hola");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, chat.StatusCode);
        Assert.Equal(AssistantClient.DisabledDetail, await AssistantClient.ProblemDetailAsync(chat, 503));
        Assert.Equal(createdBefore, factory.ChatFactory.Created.Count);
    }

    private static string WithoutTraceId(string json)
    {
        using var document = JsonDocument.Parse(json);
        var members = document.RootElement.EnumerateObject().Where(property => property.Name != "traceId").Select(property => $"{property.Name}={property.Value}");
        return string.Join("|", members);
    }

    private async Task<string?> ScalarAsync(string sql) => (await QueryAsync(sql)).SingleOrDefault();

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
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
