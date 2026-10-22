using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Support.EmployeesHttp;
using static TimeTracker.Api.IntegrationTests.Support.TwoCompanyFixture;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// <c>POST /api/auth/change-password</c> (employee-management y authentication): permitido con el cambio pendiente, compañía activa
/// antes de cambiar nada, token <c>xmin</c>, 5 por minuto por usuario y sin eco de contraseñas. Valores inventados.
/// </summary>
[Collection("Postgres")]
public class ChangePasswordEndpointTests(PostgresFixture postgres)
{
    private const string NewPassword = "contrasena-prueba-12345";

    // El hash de UserSeed corresponde a esta contraseña.
    private const string SeededPassword = UserSeed.Password;

    private static string NewEmail() => $"persona.{Guid.NewGuid():N}@example.test";

    // Persona con cambio pendiente que entra con la contraseña sembrada: devuelve su cliente con el token con mcp.
    private static async Task<(Guid Id, string Email, HttpClient Client)> PendingPersonAsync(
        EmployeesApiFactory factory, string connectionString, Guid companyId, Guid? managerId = null)
    {
        var id = Guid.NewGuid();
        var email = NewEmail();
        await UserSeed.InsertRawAsync(connectionString, id, email, managerId: managerId, companyId: companyId);
        await SetMustChangePasswordAsync(connectionString, id, true);
        var session = await LoginOkAsync(factory, email, SeededPassword);
        return (id, email, WithToken(factory, session.GetProperty("accessToken").GetString()!));
    }

    [Fact]
    public async Task ThePendingPerson_ChangesThePassword_GetsATokenWithoutMcp_AndCanUseTheApi()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var (id, email, pending) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id);
        // Control: antes del cambio el token con mcp está bloqueado.
        Assert.Equal(HttpStatusCode.Forbidden, (await pending.GetAsync("/api/time-entries", CancellationToken.None)).StatusCode);

        var response = await ChangePasswordAsync(pending, SeededPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal(["accessToken", "expiresAt", "user"], body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.False(body.RootElement.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("companyId", out _));
        var token = body.RootElement.GetProperty("accessToken").GetString()!;
        var payload = RegistrationApiFactory.TokenPayload(token);
        Assert.False(payload.TryGetProperty("mcp", out _));
        Assert.Equal(fixture.A.Id.ToString(), payload.GetProperty("cid").GetString());
        Assert.Equal(
            Assert.Single(await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT name FROM companies WHERE id = '{fixture.A.Id}'")),
            payload.GetProperty("cname").GetString());
        Assert.Equal(["false"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT must_change_password::text FROM users WHERE id = '{id}'"));
        Assert.Equal(HttpStatusCode.OK, (await WithToken(factory, token).GetAsync("/api/time-entries", CancellationToken.None)).StatusCode);
        // La contraseña anterior deja de servir y la nueva entra sin cambio pendiente.
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(factory, email, SeededPassword)).StatusCode);
        var session = await LoginOkAsync(factory, email, NewPassword);
        Assert.False(session.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
    }

    [Theory]
    [InlineData("no-es-la-actual-123", NewPassword, "currentPassword")]
    [InlineData("", NewPassword, "currentPassword")]
    [InlineData(SeededPassword, "corta-1234", "newPassword")]
    [InlineData(SeededPassword, "contrasena-de-129-caracteres", "newPassword")]
    [InlineData(SeededPassword, SeededPassword, "newPassword")]
    public async Task InvalidPasswords_Give400WithTheField_AndNothingChanges(string current, string next, string field)
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var (id, _, pending) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.A.Id);
        var before = await HashOfAsync(fixture.ConnectionString, id);
        // La variante de 129 caracteres se construye aquí para que la tabla de casos no repita 129 letras.
        next = next == "contrasena-de-129-caracteres" ? new string('n', 129) : next;

        var response = await ChangePasswordAsync(pending, current, next);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal([field], body.RootElement.GetProperty("errors").EnumerateObject().Select(property => property.Name));
        Assert.Equal(before, await HashOfAsync(fixture.ConnectionString, id));
        Assert.Equal(["true"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT must_change_password::text FROM users WHERE id = '{id}'"));
    }

    [Fact]
    public async Task ANewPasswordOf11And12Characters_AreRejectedAndAccepted()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var (_, _, pending) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.A.Id);

        var eleven = await ChangePasswordAsync(pending, SeededPassword, new string('a', 11));
        var twelve = await ChangePasswordAsync(pending, SeededPassword, new string('a', 12));

        Assert.Equal(HttpStatusCode.BadRequest, eleven.StatusCode);
        Assert.Equal(HttpStatusCode.OK, twelve.StatusCode);
    }

    [Fact]
    public async Task WithoutAToken_Gives401()
    {
        using var factory = new EmployeesApiFactory(postgres.ConnectionString);

        var response = await ChangePasswordAsync(factory.CreateAnonymousClient(), SeededPassword, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASuspendedCompanyWithAValidSession_Gets401_NoTokenAndTheHashDoesNotChange_WhileAnActiveOneChanges()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var (suspendedId, _, suspended) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.A.Id);
        var (activeId, _, active) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.B.Id);
        var before = await HashOfAsync(fixture.ConnectionString, suspendedId);
        await MigrationDatabase.ExecuteAsync(fixture.ConnectionString, $"UPDATE companies SET status = 'Suspended' WHERE id = '{fixture.A.Id}'");

        var rejected = await ChangePasswordAsync(suspended, SeededPassword, NewPassword);
        var accepted = await ChangePasswordAsync(active, SeededPassword, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        var text = await rejected.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain("accessToken", text, StringComparison.Ordinal);
        Assert.Contains("Credenciales no válidas", text, StringComparison.Ordinal);
        Assert.Equal(before, await HashOfAsync(fixture.ConnectionString, suspendedId));
        Assert.Equal(["true"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT must_change_password::text FROM users WHERE id = '{suspendedId}'"));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.NotEqual(SeededPassword, await HashOfAsync(fixture.ConnectionString, activeId));
    }

    [Fact]
    public async Task WhenTheManagerRegeneratesBetweenReadAndSave_ThePersonGets409_AndTheNewTemporaryIsNotOverwritten()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var id = Guid.NewGuid();
        var email = NewEmail();
        await UserSeed.InsertRawAsync(fixture.ConnectionString, id, email, managerId: fixture.A.Manager.Id, companyId: fixture.A.Id);
        await SetMustChangePasswordAsync(fixture.ConnectionString, id, true);
        var regenerated = $"hash-regenerado-por-el-manager-{Guid.NewGuid():N}";
        var race = new OneShotRacingInterceptor(fixture.ConnectionString, $"UPDATE users SET password_hash = '{regenerated}' WHERE id = '{id}'");
        using var factory = new EmployeesApiFactory(fixture.ConnectionString, interceptor: race);
        var session = await LoginOkAsync(factory, email, SeededPassword);
        var pending = WithToken(factory, session.GetProperty("accessToken").GetString()!);

        var response = await ChangePasswordAsync(pending, SeededPassword, NewPassword);

        Assert.True(race.Fired, "La carrera debía ocurrir justo antes del guardado de la persona.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal($"{regenerated}|true", Assert.Single(await MigrationDatabase.QueryAsync(
            fixture.ConnectionString, $"SELECT password_hash || '|' || must_change_password::text FROM users WHERE id = '{id}'")));
    }

    [Fact]
    public async Task TheRateLimit_Is5PerMinutePerUser_With429RetryAfterAndCors_AndAnotherUserIsNotAffected()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var (id, _, limited) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.A.Id);
        var (_, _, other) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.A.Id);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await ChangePasswordAsync(limited, "no-es-la-actual-123", NewPassword)).StatusCode);
        }

        var before = await HashOfAsync(fixture.ConnectionString, id);
        var sixth = await SendWithOriginAsync(
            limited, HttpMethod.Post, ChangePasswordPath, JsonContent.Create(new { currentPassword = SeededPassword, newPassword = NewPassword }));
        var otherUser = await ChangePasswordAsync(other, SeededPassword, NewPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.True(sixth.Headers.Contains("Retry-After"));
        Assert.Equal([ApiFactory.ValidOrigin], sixth.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Equal(before, await HashOfAsync(fixture.ConnectionString, id));
        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
    }

    [Fact]
    public async Task UserIdAndCompanyIdInTheBody_AreIgnored_OnlyTheUserOfTheTokenChanges()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var (id, _, pending) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.A.Id);
        var victimHash = await HashOfAsync(fixture.ConnectionString, fixture.B.Employee1.Id);
        var ownHashBefore = await HashOfAsync(fixture.ConnectionString, id);

        var response = await ChangePasswordAsync(pending, SeededPassword, NewPassword, new { userId = fixture.B.Employee1.Id, companyId = fixture.B.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(victimHash, await HashOfAsync(fixture.ConnectionString, fixture.B.Employee1.Id));
        Assert.NotEqual(ownHashBefore, await HashOfAsync(fixture.ConnectionString, id));
    }

    [Fact]
    public async Task NeitherPasswordAppearsInResponsesOrLogs_ForCorrectAndIncorrectChanges()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var (_, _, pending) = await PendingPersonAsync(factory, fixture.ConnectionString, fixture.A.Id);
        const string Wrong = "actual-incorrecta-98765";

        var incorrect = await ChangePasswordAsync(pending, Wrong, NewPassword);
        var correct = await ChangePasswordAsync(pending, SeededPassword, NewPassword);

        var texts = new[] { await incorrect.Content.ReadAsStringAsync(CancellationToken.None), await correct.Content.ReadAsStringAsync(CancellationToken.None) };
        var logs = string.Join('\n', factory.Logs.Messages);
        Assert.NotEmpty(factory.Logs.Messages);
        foreach (var secret in new[] { Wrong, NewPassword, SeededPassword })
        {
            Assert.All(texts, text => Assert.DoesNotContain(secret, text, StringComparison.Ordinal));
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheTemporaryPassword_StopsWorkingAfterTheChange()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var email = NewEmail();
        using var created = await ReadAsync(await CreateAsync(manager, new { displayName = "Luis", email }));
        var temporary = created.RootElement.GetProperty("temporaryPassword").GetString()!;
        var session = await LoginOkAsync(factory, email, temporary);

        var response = await ChangePasswordAsync(WithToken(factory, session.GetProperty("accessToken").GetString()!), temporary, NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await LoginAsync(factory, email, temporary);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Contains("Credenciales no válidas", await login.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
    }
}
