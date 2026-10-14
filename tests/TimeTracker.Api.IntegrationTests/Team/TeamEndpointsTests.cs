using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Security;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Team;

[Collection("Postgres")]
public class TeamEndpointsTests(PostgresFixture postgres)
{
    private const string Members = "/api/team/members";
    private const string TimeEntries = "/api/time-entries";

    [Fact]
    public async Task Members_AsEmployee_Returns403ProblemJsonInSpanish()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(Guid.NewGuid(), "Employee").GetAsync(Members, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = await AccessErrorsTests.AssertProblemAsync(response, 403, "Acceso denegado");
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MemberEntries_AsEmployee_Returns403_ForExistingAndUnknownMember(bool existingMember)
    {
        // El rol se comprueba antes que el miembro: un empleado recibe lo mismo exista o no el miembro.
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var memberId = existingMember ? scenario.Luis.Id : Guid.NewGuid();

        var response = await factory.CreateClientFor(scenario.Ana.Id, "Employee").GetAsync($"{Members}/{memberId}/time-entries", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        (await AccessErrorsTests.AssertProblemAsync(response, 403, "Acceso denegado")).Dispose();
    }

    [Fact]
    public async Task Members_WithoutToken_Returns401()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateAnonymousClient().GetAsync(Members, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Members_AsManager_Returns200()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(Guid.NewGuid(), "Manager").GetAsync(Members, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Members_WithUnknownRoleToken_Returns401()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(Guid.NewGuid(), "Admin").GetAsync(Members, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_HasCorsOrigin()
    {
        const string webOrigin = "http://localhost:5172";
        using var baseFactory = new PostgresApiFactory(postgres.ConnectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", webOrigin));
        var request = new HttpRequestMessage(HttpMethod.Get, Members);
        request.Headers.Add("Origin", webOrigin);
        request.Headers.Authorization = new("Bearer", baseFactory.TokenFor(Guid.NewGuid(), "Employee"));

        var response = await factory.CreateAnonymousClient().SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal([webOrigin], response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    // --- 15.3 Listado del equipo ---

    [Fact]
    public async Task Members_ReturnsOnlyDirectReportsOrderedByName()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(factory.CreateClientFor(scenario.Marta.Id, "Manager"), Members);

        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Ana Ejemplo", "Luis Ejemplo"], items.Select(item => item.GetProperty("displayName").GetString()));
        Assert.Equal([scenario.Ana.Id, scenario.Luis.Id], items.Select(item => item.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Members_BodyHasOnlyIdAndDisplayName()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager").GetAsync(Members, CancellationToken.None);

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var body = JsonDocument.Parse(text);
        Assert.Equal(["items"], body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.All(
            body.RootElement.GetProperty("items").EnumerateArray(),
            item => Assert.Equal(["id", "displayName"], item.EnumerateObject().Select(property => property.Name)));
        foreach (var forbidden in new[] { "email", "passwordHash", "role", "managerId", scenario.Luis.Email })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Members_OfAnotherManager_DoesNotContainOthers()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(factory.CreateClientFor(scenario.Diego.Id, "Manager"), Members);

        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        var member = Assert.Single(items);
        Assert.Equal(scenario.Carmen.Id, member.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Members_WithoutReports_ReturnsEmptyItems()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(factory.CreateClientFor(Guid.NewGuid(), "Manager"), Members);

        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
    }

    // --- 15.4 Límite de 100 miembros ---

    [Fact]
    public async Task Members_With101DirectReports_ReturnsAtMost100()
    {
        var manager = UserSeed.Create("jefa", UserRole.Manager, displayName: "Jefa Ejemplo");
        var reports = Enumerable.Range(1, 101)
            .Select(number => UserSeed.Create($"miembro{number:000}", managerId: manager.Id, displayName: $"Miembro {number:000}"))
            .ToArray();
        await using (var context = postgres.CreateContext(postgres.ConnectionString))
        {
            await UserSeed.AddAsync(context, [manager, .. reports]);
        }

        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        using var body = await GetJsonAsync(factory.CreateClientFor(manager.Id, "Manager"), Members);

        var names = body.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("displayName").GetString()).ToList();
        Assert.Equal(100, names.Count);
        Assert.DoesNotContain("Miembro 101", names);
    }

    // --- 15.5 Fichajes de un miembro ---

    private static string EntriesOf(Guid memberId) => $"{Members}/{memberId}/time-entries";

    [Fact]
    public async Task MemberEntries_ForDirectReport_Returns200WithTotalAndZTimestamps()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        var start = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, start, minutes: 60);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, start.AddDays(1), minutes: 30);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, start.AddDays(2), minutes: null);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager").GetAsync(EntriesOf(scenario.Luis.Id), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(90, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        // El más reciente primero: el abierto no tiene salida ni minutos trabajados.
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("clockOutAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("durationMinutes").ValueKind);
        Assert.EndsWith("Z", items[0].GetProperty("clockInAt").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("Z", items[1].GetProperty("clockOutAt").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MemberEntries_With305ClosedOf10Minutes_Returns300AndTotal3000()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        var start = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
        await TeamScenario.AddClosedDailyEntriesAsync(postgres, scenario.Luis.Id, start, count: 305, minutes: 10);

        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        using var body = await GetJsonAsync(factory.CreateClientFor(scenario.Marta.Id, "Manager"), EntriesOf(scenario.Luis.Id));

        Assert.Equal(300, body.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(3000, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
    }

    [Fact]
    public async Task MemberEntries_WithoutEntries_ReturnsEmptyAndZero()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(factory.CreateClientFor(scenario.Marta.Id, "Manager"), EntriesOf(scenario.Ana.Id));

        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
    }

    [Fact]
    public async Task MemberEntries_ContainsOnlyTheMembersEntries()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        var at = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, at, minutes: 15);
        await TeamScenario.AddEntryAsync(postgres, scenario.Ana.Id, at, minutes: 20);
        await TeamScenario.AddEntryAsync(postgres, scenario.Marta.Id, at, minutes: 25);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(factory.CreateClientFor(scenario.Marta.Id, "Manager"), EntriesOf(scenario.Luis.Id));

        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(15, item.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(15, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
    }

    [Fact]
    public async Task MemberEntries_IncludeBreaksWithTypeTotalsAndStatus()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        var start = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, start, minutes: 60);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, start.AddHours(1), minutes: 15, TimeEntryType.Break);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, start.AddHours(2), minutes: 45, TimeEntryType.MealBreak);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, start.AddHours(3), minutes: null, TimeEntryType.Break);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(factory.CreateClientFor(scenario.Marta.Id, "Manager"), EntriesOf(scenario.Luis.Id));

        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Break", "MealBreak", "Break", "Work"], items.Select(item => item.GetProperty("type").GetString()));
        Assert.Equal(60, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
        Assert.Equal(15, body.RootElement.GetProperty("totalBreakMinutes").GetInt32());
        Assert.Equal(45, body.RootElement.GetProperty("totalMealBreakMinutes").GetInt32());
        Assert.Equal("OnBreak", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MemberEntries_BreaksOfAForeignMember_Return404()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        var at = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        await TeamScenario.AddEntryAsync(postgres, scenario.Carmen.Id, at, minutes: null, TimeEntryType.MealBreak);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager").GetAsync(EntriesOf(scenario.Carmen.Id), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- 15.6 Solo lectura ---

    [Fact]
    public async Task MemberEntries_Post_WithManagerToken_Returns405_AndLeavesTheEntryOpen()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero), minutes: null);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager").PostAsync(EntriesOf(scenario.Luis.Id), content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        var entry = Assert.Single(await LoadEntriesAsync(scenario.Luis.Id));
        Assert.True(entry.IsOpen);
        Assert.Empty(await LoadEntriesAsync(scenario.Marta.Id));
    }

    [Fact]
    public async Task MemberEntries_Post_WithoutToken_Returns401_AndChangesNothing()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero), minutes: null);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateAnonymousClient().PostAsync(EntriesOf(scenario.Luis.Id), content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(Assert.Single(await LoadEntriesAsync(scenario.Luis.Id)).IsOpen);
    }

    [Theory]
    [InlineData("start-break")]
    [InlineData("start-meal-break")]
    [InlineData("end-break")]
    [InlineData("clock-out")]
    public async Task Team_HasNoWriteEndpointsForBreaks_AndLeavesTheMemberUntouched(string action)
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero), minutes: null);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager")
            .PostAsync($"/api/team/members/{scenario.Luis.Id}/{action}", content: null, CancellationToken.None);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        var entry = Assert.Single(await LoadEntriesAsync(scenario.Luis.Id));
        Assert.True(entry.IsOpen);
        Assert.Equal(TimeEntryType.Work, entry.Type);
    }

    private async Task<List<TimeEntry>> LoadEntriesAsync(Guid employeeId)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await context.TimeEntries.AsNoTracking().Where(entry => entry.EmployeeId == employeeId).ToListAsync(CancellationToken.None);
    }

    // --- 15.7 Control por objeto con 404 idéntico ---

    [Fact]
    public async Task MemberEntries_OfAnotherManagersMember_Returns404ProblemJson()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero), minutes: 30);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Diego.Id, "Manager").GetAsync(EntriesOf(scenario.Luis.Id), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = await AccessErrorsTests.AssertProblemAsync(response, 404, "Recurso no encontrado");
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain("totalWorkedMinutes", body.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MemberEntries_OfUnknownMember_Returns404ProblemJson()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager").GetAsync(EntriesOf(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        (await AccessErrorsTests.AssertProblemAsync(response, 404, "Recurso no encontrado")).Dispose();
    }

    [Fact]
    public async Task MemberEntries_NotFoundResponses_AreIdentical()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClientFor(scenario.Marta.Id, "Manager");

        // Miembro ajeno, inexistente, sin responsable y el propio responsable: indistinguibles (BOLA, enumeración).
        var responses = new List<HttpResponseMessage>();
        foreach (var memberId in new[] { scenario.Carmen.Id, Guid.NewGuid(), scenario.Pablo.Id, scenario.Marta.Id })
        {
            responses.Add(await client.GetAsync(EntriesOf(memberId), CancellationToken.None));
        }

        var shapes = new List<string>();
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            shapes.Add($"{response.Content.Headers.ContentType?.MediaType}|{await BodyWithoutTraceIdAsync(response)}");
        }

        Assert.Single(shapes.Distinct(StringComparer.Ordinal));
        Assert.Contains("Recurso no encontrado", shapes[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task MemberEntries_OfSelf_Returns404()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        await TeamScenario.AddEntryAsync(postgres, scenario.Marta.Id, new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero), minutes: 30);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager").GetAsync(EntriesOf(scenario.Marta.Id), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MemberEntries_WithNonGuidId_Returns404()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var path = $"{Members}/no-es-un-guid/time-entries";

        var withToken = await factory.CreateClientFor(Guid.NewGuid(), "Manager").GetAsync(path, CancellationToken.None);
        var withoutToken = await factory.CreateAnonymousClient().GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, withToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
    }

    // «Idéntico» significa idéntico salvo traceId, que cambia en cada petición.
    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        return string.Join(",", body.RootElement.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .Select(property => $"{property.Name}={property.Value.GetRawText()}"));
    }

    // --- 15.8 Un empleado nunca ve datos ajenos ---

    [Fact]
    public async Task Employee_ListsOnlyOwnEntries()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        var at = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, at, minutes: 10);
        await TeamScenario.AddEntryAsync(postgres, scenario.Ana.Id, at, minutes: 20);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(factory.CreateClientFor(scenario.Luis.Id, "Employee"), TimeEntries);

        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(10, item.GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public async Task Manager_TimeEntriesList_ContainsOnlyOwnEntries()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        var at = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        await TeamScenario.AddEntryAsync(postgres, scenario.Marta.Id, at, minutes: 40);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, at, minutes: 10);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(factory.CreateClientFor(scenario.Marta.Id, "Manager"), TimeEntries);

        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(40, item.GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public async Task GetTimeEntries_WithMemberIdAndEmployeeIdQuery_IgnoresThem()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        var at = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, at, minutes: 10);
        await TeamScenario.AddEntryAsync(postgres, scenario.Ana.Id, at, minutes: 20);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        using var body = await GetJsonAsync(
            factory.CreateClientFor(scenario.Luis.Id, "Employee"),
            $"{TimeEntries}?memberId={scenario.Ana.Id}&employeeId={scenario.Ana.Id}");

        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(10, item.GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public async Task Manager_ClockInWithMemberIdQuery_CreatesOwnEntry()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager")
            .PostAsync($"{TimeEntries}/clock-in?memberId={scenario.Luis.Id}", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(await LoadEntriesAsync(scenario.Marta.Id));
        Assert.Empty(await LoadEntriesAsync(scenario.Luis.Id));
    }

    [Fact]
    public async Task Manager_CanClockIn_Returns201()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(Guid.NewGuid(), "Manager").PostAsync($"{TimeEntries}/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
    }
}
