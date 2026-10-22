using System.Net;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Assistant;
using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;
using static TimeTracker.Api.IntegrationTests.Support.EmployeesHttp;
using static TimeTracker.Api.IntegrationTests.Support.TwoCompanyFixture;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// TC-15 (employee-management, «Creación de otros Managers solo dentro de la compañía»): un Manager nuevo nace en la compañía de quien
/// lo crea, sin acceso a la otra y sin jerarquía transitiva. Sin proveedor real de IA: fábrica y probador falsos.
/// </summary>
[Collection("Postgres")]
public class ManagerCreationIsolationTests(PostgresFixture postgres)
{
    private const string NewPassword = "contrasena-prueba-12345";
    private const string ValidPut = """{"provider":"OpenAI","model":"modelo-de-a","timeoutSeconds":20,"maxOutputTokens":500,"credential":"clave-prueba-123456","concurrencyToken":"0"}""";

    private static string NewEmail() => $"manager.{Guid.NewGuid():N}@example.test";

    // Crea una persona por la API y devuelve su cliente ya con la contraseña cambiada (sesión sin mcp).
    private static async Task<(Guid Id, HttpClient Client, JsonElement Session)> CreateAndActivateAsync(
        AssistantConfigurationApiFactory factory, HttpClient creator, string role, Guid? managerId = null)
    {
        var email = NewEmail();
        var response = await CreateAsync(creator, managerId is null ? new { displayName = "Nuevo", email, role } : new { displayName = "Nuevo", email, role, managerId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var created = await ReadAsync(response);
        var temporary = created.RootElement.GetProperty("temporaryPassword").GetString()!;
        var pending = await LoginOkAsync(factory, email, temporary);
        var changed = await ChangePasswordAsync(WithToken(factory, pending.GetProperty("accessToken").GetString()!), temporary, NewPassword);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var body = await ReadAsync(changed);
        var session = body.RootElement.Clone();
        return (created.RootElement.GetProperty("id").GetGuid(), WithToken(factory, session.GetProperty("accessToken").GetString()!), session);
    }

    [Fact]
    public async Task ANewManager_ReportsToTheCreator_HasTheRoleAndCompanyOfTheCreator_AndSeesOnlyHisCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var managerA = ClientFor(factory, fixture.A, fixture.A.Manager);
        var managerB = ClientFor(factory, fixture.B, fixture.B.Manager);
        Assert.Equal(HttpStatusCode.OK, (await managerA.PutJsonAsync(ValidPut)).StatusCode);

        var (newId, newManager, session) = await CreateAndActivateAsync(factory, managerA, "Manager");

        Assert.Equal([$"{fixture.A.Manager.Id}"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT manager_id::text FROM users WHERE id = '{newId}'"));
        var payload = RegistrationApiFactory.TokenPayload(session.GetProperty("accessToken").GetString()!);
        Assert.Equal("Manager", payload.GetProperty("role").GetString());
        Assert.Equal(fixture.A.Id.ToString(), payload.GetProperty("cid").GetString());
        // Su equipo es el suyo (vacío) y nunca contiene a nadie de B; en el de su creador sí aparece.
        using var ownTeam = await ReadAsync(await newManager.GetAsync(TeamMembersPath, CancellationToken.None));
        Assert.Empty(ownTeam.RootElement.GetProperty("items").EnumerateArray());
        using var creatorTeam = await ReadAsync(await managerA.GetAsync(TeamMembersPath, CancellationToken.None));
        Assert.Contains(newId, creatorTeam.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        // La configuración del asistente que ve es la de A (la que guardó su creador) y nunca la de B.
        Assert.Equal(HttpStatusCode.OK, (await managerB.PutJsonAsync(ValidPut.Replace("modelo-de-a", "modelo-de-b", StringComparison.Ordinal))).StatusCode);
        var configuration = await newManager.GetAsync(Route, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, configuration.StatusCode);
        var text = await configuration.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("modelo-de-a", text, StringComparison.Ordinal);
        Assert.DoesNotContain("modelo-de-b", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThereIsNoTransitiveHierarchy_TheTeamOfM1ContainsM2AndNotTheEmployeeCreatedByM2()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var m1 = ClientFor(factory, fixture.A, fixture.A.Manager);

        var (m2Id, m2, _) = await CreateAndActivateAsync(factory, m1, "Manager");
        var (employeeId, _, _) = await CreateAndActivateAsync(factory, m2, "Employee");

        using var teamOfM1 = await ReadAsync(await m1.GetAsync(TeamMembersPath, CancellationToken.None));
        var ids = teamOfM1.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(m2Id, ids);
        Assert.DoesNotContain(employeeId, ids);
        using var teamOfM2 = await ReadAsync(await m2.GetAsync(TeamMembersPath, CancellationToken.None));
        Assert.Equal([employeeId], teamOfM2.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task ANewManager_CannotCreateAPersonUnderAManagerOfTheOtherCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new AssistantConfigurationApiFactory(fixture.ConnectionString);
        var (_, newManager, _) = await CreateAndActivateAsync(factory, ClientFor(factory, fixture.A, fixture.A.Manager), "Manager");
        var before = await CountUsersAsync(fixture.ConnectionString, fixture.A.Id);

        var response = await CreateAsync(newManager, new { displayName = "Intruso", email = NewEmail(), managerId = fixture.B.Manager.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountUsersAsync(fixture.ConnectionString, fixture.A.Id));
    }
}
