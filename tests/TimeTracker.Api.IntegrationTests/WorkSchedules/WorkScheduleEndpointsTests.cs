using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Application.WorkSchedules;

using static TimeTracker.Api.IntegrationTests.Support.TwoCompanyFixture;

namespace TimeTracker.Api.IntegrationTests.WorkSchedules;

/// <summary>Jornadas de trabajo: solo Manager, aislamiento por compañía (A no ve ni toca las de B), validación, tope y asignación.</summary>
[Collection("Postgres")]
public class WorkScheduleEndpointsTests(PostgresFixture postgres)
{
    private const string Path = "/api/work-schedules";

    private static JsonObject Body(string name = "Oficina") => new()
    {
        ["name"] = name,
        ["startTime"] = "09:00:00",
        ["lunchStart"] = "14:00:00",
        ["lunchMinutes"] = 60,
        ["expectedBreakMinutes"] = 30,
        ["days"] = new JsonArray(1, 2, 3, 4, 5),
    };

    private static async Task<Guid> CreateAsync(HttpClient manager, string name = "Oficina")
    {
        var response = await manager.PostAsJsonAsync(Path, Body(name), CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ListAsync(HttpClient client)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync(Path, CancellationToken.None));
        return json.RootElement.GetProperty("items").Clone();
    }

    [Fact]
    public async Task AnonymousRequests_Return401()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var anonymous = factory.CreateAnonymousClient();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path, CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Path, Body(), CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"{Path}/{id}", Body(), CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"{Path}/{id}", CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Path}/mine", CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"/api/team/members/{id}/work-schedule", new JsonObject(), CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task Employees_CannotManageSchedules_Return403()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var employee = ClientFor(factory, fixture.A, fixture.A.Employee1);
        var id = await CreateAsync(ClientFor(factory, fixture.A, fixture.A.Manager));

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync(Path, CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync(Path, Body("Otra"), CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PutAsJsonAsync($"{Path}/{id}", Body("Otra"), CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.DeleteAsync($"{Path}/{id}", CancellationToken.None)).StatusCode);
        var assign = await employee.PutAsJsonAsync($"/api/team/members/{fixture.A.Employee2.Id}/work-schedule", new JsonObject { ["workScheduleId"] = id.ToString() }, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
    }

    [Fact]
    public async Task Manager_CreatesListsUpdatesAndDeletes()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);

        var created = await manager.PostAsJsonAsync(Path, Body(" Oficina "), CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(CancellationToken.None));
        var id = createdJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Oficina", createdJson.RootElement.GetProperty("name").GetString());
        Assert.Equal("09:00:00", createdJson.RootElement.GetProperty("startTime").GetString());
        Assert.Equal(60, createdJson.RootElement.GetProperty("lunchMinutes").GetInt32());
        Assert.Equal(0, createdJson.RootElement.GetProperty("assignedCount").GetInt32());
        Assert.Equal([1, 2, 3, 4, 5], createdJson.RootElement.GetProperty("days").EnumerateArray().Select(day => day.GetInt32()));
        Assert.DoesNotContain("companyId", await created.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.OrdinalIgnoreCase);

        var updatedBody = Body("Oficina tarde");
        updatedBody["startTime"] = "15:00:00";
        updatedBody["lunchStart"] = "18:00:00";
        updatedBody["days"] = new JsonArray(0, 6);
        var updated = await manager.PutAsJsonAsync($"{Path}/{id}", updatedBody, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var items = await ListAsync(manager);
        var item = Assert.Single(items.EnumerateArray(), candidate => candidate.GetProperty("id").GetGuid() == id);
        Assert.Equal("Oficina tarde", item.GetProperty("name").GetString());
        Assert.Equal("15:00:00", item.GetProperty("startTime").GetString());
        Assert.Equal([6, 0], item.GetProperty("days").EnumerateArray().Select(day => day.GetInt32()));

        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"{Path}/{id}", CancellationToken.None)).StatusCode);
        Assert.DoesNotContain((await ListAsync(manager)).EnumerateArray(), candidate => candidate.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task InvalidBodies_Return400WithFieldErrors()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var invalid = Body("");
        invalid["lunchStart"] = "08:00:00";
        invalid["lunchMinutes"] = 481;
        invalid["days"] = new JsonArray();

        var response = await manager.PostAsJsonAsync(Path, invalid, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        var errors = json.RootElement.GetProperty("errors");
        foreach (var field in new[] { "name", "lunchStart", "lunchMinutes", "days" })
        {
            Assert.True(errors.TryGetProperty(field, out _), field);
        }

        var empty = await manager.PostAsync(Path, JsonContent.Create(new JsonObject()), CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync(Path, new StringContent("null", System.Text.Encoding.UTF8, "application/json"), CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task ADuplicateName_ReturnsConflict_ButTheSameNameIsFineInAnotherCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var managerA = ClientFor(factory, fixture.A, fixture.A.Manager);
        var managerB = ClientFor(factory, fixture.B, fixture.B.Manager);
        await CreateAsync(managerA, "Oficina");

        var duplicate = await managerA.PostAsJsonAsync(Path, Body("OFICINA"), CancellationToken.None);
        var other = await managerB.PostAsJsonAsync(Path, Body("Oficina"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }

    [Fact]
    public async Task TheCompanyCap_Returns409WhenExceeded()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        for (var index = 0; index < WorkScheduleLimits.MaxPerCompany; index++)
        {
            await CreateAsync(manager, $"Jornada {index}");
        }

        var response = await manager.PostAsJsonAsync(Path, Body("Una más"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(WorkScheduleLimits.MaxPerCompany, (await ListAsync(manager)).GetArrayLength());
    }

    [Fact]
    public async Task ASchedule_OfAnotherCompany_IsNotVisibleNorTouchable()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var managerA = ClientFor(factory, fixture.A, fixture.A.Manager);
        var managerB = ClientFor(factory, fixture.B, fixture.B.Manager);
        var idOfB = await CreateAsync(managerB, "Solo de B");
        var unknown = Guid.NewGuid();

        Assert.DoesNotContain((await ListAsync(managerA)).EnumerateArray(), item => item.GetProperty("id").GetGuid() == idOfB);

        var putOfB = await managerA.PutAsJsonAsync($"{Path}/{idOfB}", Body("Robada"), CancellationToken.None);
        var putUnknown = await managerA.PutAsJsonAsync($"{Path}/{unknown}", Body("Robada"), CancellationToken.None);
        var deleteOfB = await managerA.DeleteAsync($"{Path}/{idOfB}", CancellationToken.None);
        var deleteUnknown = await managerA.DeleteAsync($"{Path}/{unknown}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, putOfB.StatusCode);
        Assert.Equal(putUnknown.StatusCode, putOfB.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteOfB.StatusCode);
        Assert.Equal(deleteUnknown.StatusCode, deleteOfB.StatusCode);
        Assert.Contains((await ListAsync(managerB)).EnumerateArray(), item => item.GetProperty("name").GetString() == "Solo de B");
    }

    [Fact]
    public async Task AssigningAndUnassigning_UpdatesTheCountTheTeamListAndMine()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var employee = ClientFor(factory, fixture.A, fixture.A.Employee1);
        var id = await CreateAsync(manager);
        var assignPath = $"/api/team/members/{fixture.A.Employee1.Id}/work-schedule";

        var withoutSchedule = await employee.GetAsync($"{Path}/mine", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, withoutSchedule.StatusCode);

        var assigned = await manager.PutAsJsonAsync(assignPath, new JsonObject { ["workScheduleId"] = id.ToString() }, CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);

        var item = Assert.Single((await ListAsync(manager)).EnumerateArray(), candidate => candidate.GetProperty("id").GetGuid() == id);
        Assert.Equal(1, item.GetProperty("assignedCount").GetInt32());
        using var members = JsonDocument.Parse(await manager.GetStringAsync("/api/team/members", CancellationToken.None));
        var member = Assert.Single(members.RootElement.GetProperty("items").EnumerateArray(), candidate => candidate.GetProperty("id").GetGuid() == fixture.A.Employee1.Id);
        Assert.Equal(id, member.GetProperty("workScheduleId").GetGuid());

        var mine = await employee.GetAsync($"{Path}/mine", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        using var mineJson = JsonDocument.Parse(await mine.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(id, mineJson.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Oficina", mineJson.RootElement.GetProperty("name").GetString());

        var unassigned = await manager.PutAsJsonAsync(assignPath, new JsonObject { ["workScheduleId"] = null }, CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, unassigned.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await employee.GetAsync($"{Path}/mine", CancellationToken.None)).StatusCode);
        Assert.Equal(0, Assert.Single((await ListAsync(manager)).EnumerateArray(), candidate => candidate.GetProperty("id").GetGuid() == id).GetProperty("assignedCount").GetInt32());
    }

    [Fact]
    public async Task DeletingAnAssignedSchedule_Returns409AndKeepsItAndTheAssignment()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var id = await CreateAsync(manager);
        await manager.PutAsJsonAsync($"/api/team/members/{fixture.A.Employee1.Id}/work-schedule", new JsonObject { ["workScheduleId"] = id.ToString() }, CancellationToken.None);

        var response = await manager.DeleteAsync($"{Path}/{id}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Contains("asignada", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single((await ListAsync(manager)).EnumerateArray(), candidate => candidate.GetProperty("id").GetGuid() == id).GetProperty("assignedCount").GetInt32());
    }

    [Fact]
    public async Task Assignment_AcrossCompanies_Returns404()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var managerA = ClientFor(factory, fixture.A, fixture.A.Manager);
        var managerB = ClientFor(factory, fixture.B, fixture.B.Manager);
        var scheduleOfA = await CreateAsync(managerA);
        var scheduleOfB = await CreateAsync(managerB, "De B");

        // Jornada de B para una persona de A, y persona de B desde A: ambas indistinguibles de lo inexistente.
        var foreignSchedule = await managerA.PutAsJsonAsync($"/api/team/members/{fixture.A.Employee1.Id}/work-schedule", new JsonObject { ["workScheduleId"] = scheduleOfB.ToString() }, CancellationToken.None);
        var foreignMember = await managerA.PutAsJsonAsync($"/api/team/members/{fixture.B.Employee1.Id}/work-schedule", new JsonObject { ["workScheduleId"] = scheduleOfA.ToString() }, CancellationToken.None);
        var unknownMember = await managerA.PutAsJsonAsync($"/api/team/members/{Guid.NewGuid()}/work-schedule", new JsonObject { ["workScheduleId"] = scheduleOfA.ToString() }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, foreignSchedule.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownMember.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ClientFor(factory, fixture.B, fixture.B.Employee1).GetAsync($"{Path}/mine", CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task Assignment_ToAPersonOutsideTheManagersTeam_Returns404()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var manager = ClientFor(factory, fixture.A, fixture.A.Manager);
        var id = await CreateAsync(manager);

        // Otro Manager de la misma compañía no es del equipo: la regla de equipo de /api/team.
        var response = await manager.PutAsJsonAsync($"/api/team/members/{fixture.A.Manager.Id}/work-schedule", new JsonObject { ["workScheduleId"] = id.ToString() }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Mine_IsAvailableToAnyRole_AndNeverShowsAnotherCompanysSchedule()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);

        Assert.Equal(HttpStatusCode.NoContent, (await ClientFor(factory, fixture.A, fixture.A.Manager).GetAsync($"{Path}/mine", CancellationToken.None)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ClientFor(factory, fixture.A, fixture.A.Employee2).GetAsync($"{Path}/mine", CancellationToken.None)).StatusCode);
    }
}
