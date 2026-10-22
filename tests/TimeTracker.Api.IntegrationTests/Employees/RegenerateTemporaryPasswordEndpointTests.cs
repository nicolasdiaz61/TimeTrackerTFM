using System.Net;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Support.EmployeesHttp;
using static TimeTracker.Api.IntegrationTests.Support.TwoCompanyFixture;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// <c>POST /api/employees/{memberId}/temporary-password</c> (V-13, TC-16): solo el responsable directo, solo antes del primer acceso,
/// sin pisar la contraseña elegida por la persona y sin eco de ninguna contraseña. Valores inventados.
/// </summary>
[Collection("Postgres")]
public class RegenerateTemporaryPasswordEndpointTests(PostgresFixture postgres)
{
    private static string NewEmail() => $"persona.{Guid.NewGuid():N}@example.test";

    // La persona la crea el Manager por la API: así se conoce su contraseña temporal anterior.
    private static async Task<(Guid Id, string Email, string Temporary)> CreatePendingAsync(HttpClient manager)
    {
        var email = NewEmail();
        var response = await CreateAsync(manager, new { displayName = "Luis Empleado", email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await ReadAsync(response);
        return (body.RootElement.GetProperty("id").GetGuid(), email, body.RootElement.GetProperty("temporaryPassword").GetString()!);
    }

    [Fact]
    public async Task TheDirectManager_RegeneratesThePassword_OnlyOnceVisible_AndTheOldOneStopsWorking()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var (id, email, previous) = await CreatePendingAsync(manager);

        var response = await RegenerateAsync(manager, id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var body = await ReadAsync(response);
        Assert.Equal(["temporaryPassword"], body.RootElement.EnumerateObject().Select(property => property.Name));
        var regenerated = body.RootElement.GetProperty("temporaryPassword").GetString()!;
        Assert.True(regenerated.Length >= 16);
        Assert.NotEqual(previous, regenerated);
        Assert.Equal(["true"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT must_change_password::text FROM users WHERE id = '{id}'"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(factory, email, previous)).StatusCode);
        var session = await LoginOkAsync(factory, email, regenerated);
        Assert.True(session.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Anonymous_Gets401_AndAnEmployee403_AndTheHashDoesNotChange()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var memberId = fixture.A.Employee1.Id;
        await SetMustChangePasswordAsync(fixture.ConnectionString, memberId, true);
        var before = await HashOfAsync(fixture.ConnectionString, memberId);

        var anonymous = await RegenerateAsync(factory.CreateAnonymousClient(), memberId);
        var employee = await RegenerateAsync(ClientFor(factory, fixture.A, fixture.A.Employee2), memberId);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employee.StatusCode);
        Assert.Equal(before, await HashOfAsync(fixture.ConnectionString, memberId));
    }

    [Fact]
    public async Task APersonOfAnotherCompany_AnUnknownId_AndAPersonOfAnotherManagerOfTheSameCompany_GiveTheSame404()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var otherManagerId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(fixture.ConnectionString, otherManagerId, NewEmail(), "Manager", companyId: fixture.A.Id);
        var ofOtherManager = Guid.NewGuid();
        await UserSeed.InsertRawAsync(fixture.ConnectionString, ofOtherManager, NewEmail(), managerId: otherManagerId, companyId: fixture.A.Id);
        foreach (var id in new[] { fixture.B.Employee1.Id, ofOtherManager })
        {
            await SetMustChangePasswordAsync(fixture.ConnectionString, id, true);
        }

        var hashOfB = await HashOfAsync(fixture.ConnectionString, fixture.B.Employee1.Id);
        var hashOfOtherManagersPerson = await HashOfAsync(fixture.ConnectionString, ofOtherManager);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);

        var unknown = await RegenerateAsync(manager, Guid.NewGuid());
        var ofB = await RegenerateAsync(manager, fixture.B.Employee1.Id);
        var ofTheOtherManager = await RegenerateAsync(manager, ofOtherManager);
        var theManagerHerself = await RegenerateAsync(manager, fixture.A.Manager.Id);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType?.MediaType);
        var expected = await FingerprintAsync(unknown);
        Assert.Equal(expected, await FingerprintAsync(ofB));
        Assert.Equal(expected, await FingerprintAsync(ofTheOtherManager));
        Assert.Equal(expected, await FingerprintAsync(theManagerHerself));
        Assert.Equal(hashOfB, await HashOfAsync(fixture.ConnectionString, fixture.B.Employee1.Id));
        Assert.Equal(hashOfOtherManagersPerson, await HashOfAsync(fixture.ConnectionString, ofOtherManager));
        // Control: el responsable directo de esa misma persona sí puede regenerarla.
        var owner = ClientFor(factory, fixture.A, new Member(otherManagerId, "n/a", TimeTracker.Domain.Users.UserRole.Manager));
        Assert.Equal(HttpStatusCode.OK, (await RegenerateAsync(owner, ofOtherManager)).StatusCode);
        Assert.NotEqual(hashOfOtherManagersPerson, await HashOfAsync(fixture.ConnectionString, ofOtherManager));
    }

    [Fact]
    public async Task APersonWhoAlreadyChangedThePassword_Gives409_AndTheHashDoesNotChange()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var memberId = fixture.A.Employee1.Id;
        var before = await HashOfAsync(fixture.ConnectionString, memberId);

        var response = await RegenerateAsync(ClientFor(factory, fixture.A, fixture.A.Manager), memberId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(before, await HashOfAsync(fixture.ConnectionString, memberId));
    }

    [Fact]
    public async Task WhenThePersonChangesThePasswordBetweenReadAndSave_TheManagerGets409_AndThePersonChoiceIsNotOverwritten()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var memberId = fixture.A.Employee1.Id;
        await SetMustChangePasswordAsync(fixture.ConnectionString, memberId, true);
        var chosen = $"hash-elegido-por-la-persona-{Guid.NewGuid():N}";
        var race = new OneShotRacingInterceptor(
            fixture.ConnectionString, $"UPDATE users SET password_hash = '{chosen}', must_change_password = false WHERE id = '{memberId}'");
        using var factory = new EmployeesApiFactory(fixture.ConnectionString, interceptor: race);

        var response = await RegenerateAsync(ClientFor(factory, fixture.A, fixture.A.Manager), memberId);

        Assert.True(race.Fired, "La carrera debía ocurrir justo antes del guardado del Manager.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal($"{chosen}|false", Assert.Single(await MigrationDatabase.QueryAsync(
            fixture.ConnectionString, $"SELECT password_hash || '|' || must_change_password::text FROM users WHERE id = '{memberId}'")));
    }

    [Fact]
    public async Task NeitherThePreviousNorTheNewPassword_AppearInTheLogs_NorInOtherResponses()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var (id, email, previous) = await CreatePendingAsync(manager);

        using var regenerated = await ReadAsync(await RegenerateAsync(manager, id));
        var next = regenerated.RootElement.GetProperty("temporaryPassword").GetString()!;
        var others = new[]
        {
            await manager.GetStringAsync(TeamMembersPath, CancellationToken.None),
            await (await LoginAsync(factory, email, next)).Content.ReadAsStringAsync(CancellationToken.None),
            await (await RegenerateAsync(ClientFor(factory, fixture.A, fixture.A.Employee1), id)).Content.ReadAsStringAsync(CancellationToken.None),
        };

        Assert.All(others, text =>
        {
            Assert.DoesNotContain(previous, text, StringComparison.Ordinal);
            Assert.DoesNotContain(next, text, StringComparison.Ordinal);
        });
        var logs = string.Join('\n', factory.Logs.Messages);
        Assert.NotEmpty(factory.Logs.Messages);
        Assert.DoesNotContain(previous, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(next, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRegenerationRateLimit_Is30PerMinutePerUser_With429RetryAfterAndCors_AndNothingChangesOnceLimited()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var memberId = fixture.A.Employee1.Id;
        await SetMustChangePasswordAsync(fixture.ConnectionString, memberId, true);
        // Treinta peticiones a un identificador inexistente (404) agotan la ventana sin cambiar ninguna contraseña.
        for (var attempt = 0; attempt < 30; attempt++)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await RegenerateAsync(manager, Guid.NewGuid())).StatusCode);
        }

        var before = await HashOfAsync(fixture.ConnectionString, memberId);
        var thirtyFirst = await SendWithOriginAsync(manager, HttpMethod.Post, TemporaryPasswordPath(memberId));

        Assert.Equal(HttpStatusCode.TooManyRequests, thirtyFirst.StatusCode);
        Assert.True(thirtyFirst.Headers.Contains("Retry-After"));
        Assert.Equal([ApiFactory.ValidOrigin], thirtyFirst.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Equal(before, await HashOfAsync(fixture.ConnectionString, memberId));
        // Contador propio: la política del alta no comparte ventana con la de la regeneración.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await CreateAsync(manager, new { displayName = "Luis", email = NewEmail() })).StatusCode);
    }
}
