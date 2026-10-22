using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Identity;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Support.EmployeesHttp;
using static TimeTracker.Api.IntegrationTests.Support.TwoCompanyFixture;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// <c>POST /api/employees</c> (employee-management, «Alta de personas por un Manager» y «Contraseña temporal de un solo uso»): la
/// compañía sale del token, el responsable es de la misma compañía y la contraseña temporal aparece una sola vez. Valores inventados.
/// </summary>
[Collection("Postgres")]
public class EmployeeCreationEndpointTests(PostgresFixture postgres)
{
    private static string NewEmail() => $"luis.{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task AManager_CreatesAnEmployeeInHisCompany_AndGetsTheTemporaryPasswordOnce()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var email = NewEmail();

        var response = await CreateAsync(manager, new { displayName = "  Luis Empleado ", email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var body = await ReadAsync(response);
        var root = body.RootElement;
        Assert.Equal(["displayName", "email", "id", "role", "temporaryPassword"], root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Luis Empleado", root.GetProperty("displayName").GetString());
        Assert.Equal(email, root.GetProperty("email").GetString());
        Assert.Equal("Employee", root.GetProperty("role").GetString());
        var id = root.GetProperty("id").GetGuid();
        Assert.Equal([$"{fixture.A.Id}|{fixture.A.Manager.Id}|Employee|true"], await MigrationDatabase.QueryAsync(
            fixture.ConnectionString, $"SELECT company_id || '|' || manager_id || '|' || role || '|' || must_change_password FROM users WHERE id = '{id}'"));

        using var team = await ReadAsync(await manager.GetAsync(TeamMembersPath, CancellationToken.None));
        Assert.Contains(id, team.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(root.GetProperty("temporaryPassword").GetString()!, team.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTemporaryPassword_IsStoredOnlyAsAHashThatVerifies_AndLetsThePersonLogInWithAPendingChange()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var email = NewEmail();
        using var created = await ReadAsync(await CreateAsync(ClientFor(factory, fixture.A, fixture.A.Manager), new { displayName = "Luis", email }));
        var temporary = created.RootElement.GetProperty("temporaryPassword").GetString()!;
        var id = created.RootElement.GetProperty("id").GetGuid();

        var hash = await HashOfAsync(fixture.ConnectionString, id);

        Assert.DoesNotContain(temporary, hash, StringComparison.Ordinal);
        Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<object>().VerifyHashedPassword(new object(), hash, temporary));
        var session = await LoginOkAsync(factory, email, temporary);
        Assert.True(session.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        var payload = RegistrationApiFactory.TokenPayload(session.GetProperty("accessToken").GetString()!);
        Assert.Equal("true", payload.GetProperty("mcp").GetString());
        Assert.Equal(fixture.A.Id.ToString(), payload.GetProperty("cid").GetString());
    }

    [Fact]
    public async Task ThreeCreations_ProduceThreeDifferentTemporaryPasswordsOfAtLeast16Characters()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);

        var passwords = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            using var body = await ReadAsync(await CreateAsync(manager, new { displayName = $"Persona {index}", email = NewEmail() }));
            passwords.Add(body.RootElement.GetProperty("temporaryPassword").GetString()!);
        }

        Assert.Equal(3, passwords.Distinct(StringComparer.Ordinal).Count());
        Assert.All(passwords, password => Assert.True(password.Length >= 16));
    }

    [Fact]
    public async Task Anonymous_Gets401_AndAnEmployee403BeforeTheBodyIsEvaluated_AndNothingIsCreated()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var email = NewEmail();
        var before = await CountUsersAsync(fixture.ConnectionString, fixture.A.Id);

        var anonymous = await CreateAsync(factory.CreateAnonymousClient(), new { displayName = "Luis", email });
        var employee = await CreateAsync(ClientFor(factory, fixture.A, fixture.A.Employee1), new { displayName = "Luis", email });
        // Cuerpo ilegible: si se evaluara antes de autorizar, el Employee recibiría 400 en vez de 403.
        var employeeWithBrokenBody = await ClientFor(factory, fixture.A, fixture.A.Employee1)
            .PostAsync(EmployeesPath, new StringContent("{no es json", System.Text.Encoding.UTF8, "application/json"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employee.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employeeWithBrokenBody.StatusCode);
        Assert.Equal(before, await CountUsersAsync(fixture.ConnectionString, fixture.A.Id));
    }

    [Fact]
    public async Task CompanyIdAndPasswordHashInTheBody_HaveNoEffect_AndThePersonIsCreatedInTheCompanyOfTheToken()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var email = NewEmail();
        var beforeB = await CountUsersAsync(fixture.ConnectionString, fixture.B.Id);

        var response = await CreateAsync(
            ClientFor(factory, fixture.A, fixture.A.Manager),
            new { displayName = "Luis", email, companyId = fixture.B.Id, passwordHash = "hash-impuesto-por-el-cliente", temporaryPassword = "impuesta-por-el-cliente-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.NotEqual("impuesta-por-el-cliente-1", body.RootElement.GetProperty("temporaryPassword").GetString());
        Assert.Equal([fixture.A.Id.ToString()], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT company_id::text FROM users WHERE email = '{email}'"));
        Assert.DoesNotContain("hash-impuesto", await HashOfAsync(fixture.ConnectionString, body.RootElement.GetProperty("id").GetGuid()), StringComparison.Ordinal);
        Assert.Equal(beforeB, await CountUsersAsync(fixture.ConnectionString, fixture.B.Id));
    }

    [Fact]
    public async Task AResponsibleOfAnotherCompany_AndAnUnknownOne_GiveIdentical400WithAnErrorOnManagerId()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var before = await CountUsersAsync(fixture.ConnectionString, fixture.A.Id);

        var ofB = await CreateAsync(manager, new { displayName = "Luis", email = NewEmail(), managerId = fixture.B.Manager.Id });
        var unknown = await CreateAsync(manager, new { displayName = "Luis", email = NewEmail(), managerId = Guid.NewGuid() });
        var notAManager = await CreateAsync(manager, new { displayName = "Luis", email = NewEmail(), managerId = fixture.A.Employee1.Id });

        Assert.Equal(HttpStatusCode.BadRequest, ofB.StatusCode);
        Assert.Equal(await FingerprintAsync(unknown), await FingerprintAsync(ofB));
        Assert.Equal(await FingerprintAsync(unknown), await FingerprintAsync(notAManager));
        using var body = await ReadAsync(unknown);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("managerId", out _));
        Assert.Equal(before, await CountUsersAsync(fixture.ConnectionString, fixture.A.Id));
    }

    [Fact]
    public async Task AnotherManagerOfTheSameCompany_CanBeTheResponsible()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var otherManagerId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(fixture.ConnectionString, otherManagerId, NewEmail(), "Manager", companyId: fixture.A.Id);

        var response = await CreateAsync(ClientFor(factory, fixture.A, fixture.A.Manager), new { displayName = "Luis", email = NewEmail(), managerId = otherManagerId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal([otherManagerId.ToString()], await MigrationDatabase.QueryAsync(
            fixture.ConnectionString, $"SELECT manager_id::text FROM users WHERE id = '{body.RootElement.GetProperty("id").GetGuid()}'"));
    }

    [Fact]
    public async Task AnEmailAlreadyUsedInAnotherCompany_Gives409WithAGenericDetailThatNamesNeitherCompanyNorRole()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var before = await CountUsersAsync(fixture.ConnectionString, fixture.A.Id);

        var ofOtherCompany = await CreateAsync(manager, new { displayName = "Luis", email = fixture.B.Employee1.Email });
        var ofOwnCompany = await CreateAsync(manager, new { displayName = "Luis", email = fixture.A.Employee1.Email });

        Assert.Equal(HttpStatusCode.Conflict, ofOtherCompany.StatusCode);
        Assert.Equal("application/problem+json", ofOtherCompany.Content.Headers.ContentType?.MediaType);
        Assert.Equal(await FingerprintAsync(ofOwnCompany), await FingerprintAsync(ofOtherCompany));
        using var body = await ReadAsync(ofOtherCompany);
        var detail = body.RootElement.GetProperty("detail").GetString()!;
        Assert.Equal("Ya existe una cuenta con ese correo electrónico.", detail);
        Assert.DoesNotContain(fixture.B.Name, detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Employee", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await CountUsersAsync(fixture.ConnectionString, fixture.A.Id));
    }

    [Theory]
    [InlineData("""{"displayName":" ","email":"luis@example.test"}""", "displayName")]
    [InlineData("""{"email":"luis@example.test"}""", "displayName")]
    [InlineData("""{"displayName":"Luis","email":"sin-arroba"}""", "email")]
    [InlineData("""{"displayName":"Luis"}""", "email")]
    [InlineData("""{"displayName":"Luis","email":"luis@example.test","role":"Admin"}""", "role")]
    public async Task InvalidData_Gives400WithTheAffectedField_AndCreatesNothing(string json, string field)
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var before = await CountUsersAsync(fixture.ConnectionString, fixture.A.Id);

        var response = await ClientFor(factory, fixture.A, fixture.A.Manager)
            .PostAsync(EmployeesPath, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal([field], body.RootElement.GetProperty("errors").EnumerateObject().Select(property => property.Name));
        Assert.Equal(before, await CountUsersAsync(fixture.ConnectionString, fixture.A.Id));
    }

    [Fact]
    public async Task ABrokenOrMissingBody_Gives400()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);

        var broken = await manager.PostAsync(EmployeesPath, new StringContent("{no es json", System.Text.Encoding.UTF8, "application/json"), CancellationToken.None);
        var missing = await manager.PostAsync(EmployeesPath, new StringContent("null", System.Text.Encoding.UTF8, "application/json"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task TheCreationRateLimit_Is30PerMinutePerUser_With429RetryAfterAndCors_AndAnotherManagerIsNotAffected()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var limited = ClientFor(factory, fixture.A, fixture.A.Manager);
        var before = await CountUsersAsync(fixture.ConnectionString, fixture.A.Id);
        // Cuentan todas las peticiones que llegan al endpoint, también las que acaban en 400.
        for (var attempt = 0; attempt < 30; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(limited, new { displayName = " ", email = "sin-arroba" })).StatusCode);
        }

        var thirtyFirst = await SendWithOriginAsync(limited, HttpMethod.Post, EmployeesPath, JsonContent.Create(new { displayName = "Luis", email = NewEmail() }));
        var otherManager = await CreateAsync(ClientFor(factory, fixture.B, fixture.B.Manager), new { displayName = "Luis", email = NewEmail() });

        Assert.Equal(HttpStatusCode.TooManyRequests, thirtyFirst.StatusCode);
        Assert.True(thirtyFirst.Headers.Contains("Retry-After"));
        Assert.Equal([ApiFactory.ValidOrigin], thirtyFirst.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Equal("application/problem+json", thirtyFirst.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Created, otherManager.StatusCode);
        Assert.Equal(before, await CountUsersAsync(fixture.ConnectionString, fixture.A.Id));
    }

    [Fact]
    public async Task TheTemporaryPassword_NeverAppearsInTheLogs_NorInAnyOtherResponse()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var email = NewEmail();

        using var created = await ReadAsync(await CreateAsync(manager, new { displayName = "Luis", email }));
        var temporary = created.RootElement.GetProperty("temporaryPassword").GetString()!;
        var others = new[]
        {
            await manager.GetStringAsync(TeamMembersPath, CancellationToken.None),
            await (await CreateAsync(manager, new { displayName = "Luis otra vez", email })).Content.ReadAsStringAsync(CancellationToken.None),
            await (await LoginAsync(factory, email, temporary)).Content.ReadAsStringAsync(CancellationToken.None),
        };

        Assert.All(others, text => Assert.DoesNotContain(temporary, text, StringComparison.Ordinal));
        var logs = string.Join('\n', factory.Logs.Messages);
        Assert.NotEmpty(factory.Logs.Messages);
        Assert.DoesNotContain(temporary, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", logs, StringComparison.OrdinalIgnoreCase);
    }
}
