using System.Net;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.TimeEntries;

[Collection("Postgres")]
public class BreakEndpointsTests(PostgresFixture postgres)
{
    private const string ClockIn = "/api/time-entries/clock-in";
    private const string ClockOut = "/api/time-entries/clock-out";
    private const string StartBreak = "/api/time-entries/start-break";
    private const string StartMealBreak = "/api/time-entries/start-meal-break";
    private const string EndBreak = "/api/time-entries/end-break";
    private const string TimeEntries = "/api/time-entries";

    [Fact]
    public async Task ClockIn_ReturnsWorkType()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().PostAsync(ClockIn, content: null, CancellationToken.None);

        using var body = await ReadAsync(response);
        Assert.Equal("Work", body.RootElement.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(StartBreak, "Break")]
    [InlineData(StartMealBreak, "MealBreak")]
    public async Task StartBreak_WhileWorking_Returns200WithOpenBreakAndClosesWork(string endpoint, string expectedType)
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        factory.Clock.Advance(TimeSpan.FromHours(2));

        var response = await client.PostAsync(endpoint, content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal(expectedType, body.RootElement.GetProperty("type").GetString());
        Assert.Equal("2026-10-12T10:00:00Z", body.RootElement.GetProperty("clockInAt").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("clockOutAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("durationMinutes").ValueKind);
        using var list = await ListAsync(client);
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("2026-10-12T10:00:00Z", items[1].GetProperty("clockOutAt").GetString());
        Assert.Equal(120, items[1].GetProperty("durationMinutes").GetInt32());
        Assert.Equal(expectedType == "Break" ? "OnBreak" : "OnMealBreak", list.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task FullCycle_WorkBreakWorkMealWorkClockOut_HasConsistentItemsTotalsAndStatus()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await AssertStatusAsync(client, "Idle");

        await Step(factory, client, ClockIn, HttpStatusCode.Created, TimeSpan.FromHours(2));
        await AssertStatusAsync(client, "Working");
        await Step(factory, client, StartBreak, HttpStatusCode.OK, TimeSpan.FromMinutes(15));
        await AssertStatusAsync(client, "OnBreak");
        await Step(factory, client, EndBreak, HttpStatusCode.OK, TimeSpan.FromHours(2));
        await Step(factory, client, StartMealBreak, HttpStatusCode.OK, TimeSpan.FromMinutes(45));
        await AssertStatusAsync(client, "OnMealBreak");
        await Step(factory, client, EndBreak, HttpStatusCode.OK, TimeSpan.FromHours(3));
        await Step(factory, client, ClockOut, HttpStatusCode.OK, TimeSpan.Zero);

        using var list = await ListAsync(client);
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Work", "Break", "Work", "MealBreak", "Work"], items.Select(item => item.GetProperty("type").GetString()).Reverse());
        Assert.All(items, item => Assert.NotEqual(JsonValueKind.Null, item.GetProperty("clockOutAt").ValueKind));
        Assert.Equal(7 * 60, list.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
        Assert.Equal(15, list.RootElement.GetProperty("totalBreakMinutes").GetInt32());
        Assert.Equal(45, list.RootElement.GetProperty("totalMealBreakMinutes").GetInt32());
        Assert.Equal("Idle", list.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            items.Sum(item => item.GetProperty("durationMinutes").GetInt32()),
            list.RootElement.GetProperty("totalWorkedMinutes").GetInt32()
                + list.RootElement.GetProperty("totalBreakMinutes").GetInt32()
                + list.RootElement.GetProperty("totalMealBreakMinutes").GetInt32());
    }

    [Fact]
    public async Task ClockOut_DuringBreak_ClosesItAndLeavesNoOpenEntry()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        await client.PostAsync(StartMealBreak, content: null, CancellationToken.None);
        factory.Clock.Advance(TimeSpan.FromMinutes(30));

        var response = await client.PostAsync(ClockOut, content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal("MealBreak", body.RootElement.GetProperty("type").GetString());
        Assert.Equal(30, body.RootElement.GetProperty("durationMinutes").GetInt32());
        await AssertStatusAsync(client, "Idle");
    }

    [Fact]
    public async Task ClockIn_DuringBreak_Returns409()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        await client.PostAsync(StartBreak, content: null, CancellationToken.None);

        var response = await client.PostAsync(ClockIn, content: null, CancellationToken.None);

        await TimeEntryEndpointsTests.AssertProblemAsync(response, "Ya existe un fichaje abierto");
    }

    [Theory]
    [InlineData(StartBreak)]
    [InlineData(StartMealBreak)]
    public async Task StartBreak_WithoutOpenEntry_Returns409NotWorking(string endpoint)
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().PostAsync(endpoint, content: null, CancellationToken.None);

        await TimeEntryEndpointsTests.AssertProblemAsync(response, "No está trabajando");
    }

    [Theory]
    [InlineData(StartBreak)]
    [InlineData(StartMealBreak)]
    public async Task StartBreak_WhenAlreadyOnBreak_Returns409AndKeepsTheBreak(string endpoint)
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        await client.PostAsync(StartBreak, content: null, CancellationToken.None);

        var response = await client.PostAsync(endpoint, content: null, CancellationToken.None);

        await TimeEntryEndpointsTests.AssertProblemAsync(response, "No está trabajando");
        await AssertStatusAsync(client, "OnBreak");
    }

    [Fact]
    public async Task EndBreak_WhileWorkingOrIdle_Returns409NoOpenBreak()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await TimeEntryEndpointsTests.AssertProblemAsync(
            await client.PostAsync(EndBreak, content: null, CancellationToken.None),
            "No hay ninguna pausa abierta");

        await client.PostAsync(ClockIn, content: null, CancellationToken.None);

        await TimeEntryEndpointsTests.AssertProblemAsync(
            await client.PostAsync(EndBreak, content: null, CancellationToken.None),
            "No hay ninguna pausa abierta");
        await AssertStatusAsync(client, "Working");
    }

    [Fact]
    public async Task BreakEndpoints_WithoutToken_Return401()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var anonymous = factory.CreateAnonymousClient();

        foreach (var endpoint in new[] { StartBreak, StartMealBreak, EndBreak })
        {
            var response = await anonymous.PostAsync(endpoint, content: null, CancellationToken.None);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Employees_CannotPauseOrResumeForEachOther()
    {
        using var employeeA = new PostgresApiFactory(postgres.ConnectionString);
        using var employeeB = new PostgresApiFactory(postgres.ConnectionString);
        var clientA = employeeA.CreateClient();
        var clientB = employeeB.CreateClient();
        await clientA.PostAsync(ClockIn, content: null, CancellationToken.None);
        await clientB.PostAsync(ClockIn, content: null, CancellationToken.None);
        await clientB.PostAsync(StartBreak, content: null, CancellationToken.None);

        var pauseOfB = await clientB.PostAsync(StartBreak, content: null, CancellationToken.None);
        var resumeOfA = await clientA.PostAsync(EndBreak, content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, pauseOfB.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, resumeOfA.StatusCode);
        await AssertStatusAsync(clientA, "Working");
        await AssertStatusAsync(clientB, "OnBreak");
    }

    [Fact]
    public async Task StartBreak_OfOneEmployee_DoesNotTouchAnotherEmployeeWork()
    {
        using var employeeA = new PostgresApiFactory(postgres.ConnectionString);
        using var employeeB = new PostgresApiFactory(postgres.ConnectionString);
        var clientA = employeeA.CreateClient();
        var clientB = employeeB.CreateClient();
        await clientA.PostAsync(ClockIn, content: null, CancellationToken.None);
        await clientB.PostAsync(ClockIn, content: null, CancellationToken.None);

        await clientA.PostAsync(StartBreak, content: null, CancellationToken.None);

        await AssertStatusAsync(clientA, "OnBreak");
        await AssertStatusAsync(clientB, "Working");
    }

    [Fact]
    public async Task StartBreak_TenParallel_OneSucceedsAndOnlyOneOpenBreakExists()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => client.PostAsync(StartBreak, content: null, CancellationToken.None)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        using var list = await ListAsync(client);
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Single(items, item => item.GetProperty("clockOutAt").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task ClockOutAndStartBreak_InParallel_NeverLeaveAnOpenEntryAfterAnAcceptedClockOut()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var factory = new PostgresApiFactory(postgres.ConnectionString);
            var client = factory.CreateClient();
            await client.PostAsync(ClockIn, content: null, CancellationToken.None);
            factory.Clock.Advance(TimeSpan.FromHours(1));

            var clockOut = client.PostAsync(ClockOut, content: null, CancellationToken.None);
            var pause = client.PostAsync(StartBreak, content: null, CancellationToken.None);
            var responses = await Task.WhenAll(clockOut, pause);

            using var list = await ListAsync(client);
            var open = list.RootElement.GetProperty("items").EnumerateArray()
                .Count(item => item.GetProperty("clockOutAt").ValueKind == JsonValueKind.Null);
            if (responses[0].StatusCode == HttpStatusCode.OK)
            {
                // Una salida aceptada deja siempre al empleado sin intervalo abierto, gane quien gane la carrera.
                Assert.Equal(0, open);
            }

            Assert.True(open <= 1);
        }
    }

    private static async Task Step(PostgresApiFactory factory, HttpClient client, string endpoint, HttpStatusCode expected, TimeSpan advanceAfter)
    {
        var response = await client.PostAsync(endpoint, content: null, CancellationToken.None);
        Assert.Equal(expected, response.StatusCode);
        factory.Clock.Advance(advanceAfter);
    }

    private static async Task AssertStatusAsync(HttpClient client, string expected)
    {
        using var list = await ListAsync(client);
        Assert.Equal(expected, list.RootElement.GetProperty("status").GetString());
    }

    private static async Task<JsonDocument> ListAsync(HttpClient client)
        => await ReadAsync(await client.GetAsync(TimeEntries, CancellationToken.None));

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
}
