using System.Net;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.TimeEntries;

[Collection("Postgres")]
public class TimeEntryEndpointsTests(PostgresFixture postgres)
{
    private const string ClockIn = "/api/time-entries/clock-in";
    private const string ClockOut = "/api/time-entries/clock-out";
    private const string TimeEntries = "/api/time-entries";

    [Fact]
    public async Task ClockIn_WithoutOpenEntry_Returns201WithBody()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().PostAsync(ClockIn, content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        // Se lee el JSON crudo: un DateTimeOffset se escribiría con +00:00 en lugar de Z (V13).
        Assert.Equal("2026-10-12T08:00:00Z", body.RootElement.GetProperty("clockInAt").GetString());
        Assert.True(Guid.TryParse(body.RootElement.GetProperty("id").GetString(), out _));
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("clockOutAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("durationMinutes").ValueKind);
    }

    [Fact]
    public async Task ClockIn_WhenOpenEntryExists_Returns409ProblemJsonInSpanish()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);

        var response = await client.PostAsync(ClockIn, content: null, CancellationToken.None);

        await AssertProblemAsync(response, "Ya existe un fichaje abierto");
    }

    internal static async Task AssertProblemAsync(HttpResponseMessage response, string expectedTitle)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, body.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task ClockOut_WithOpenEntry_Returns200WithClosedEntry()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        factory.Clock.Advance(TimeSpan.FromHours(8));

        var response = await client.PostAsync(ClockOut, content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal("2026-10-12T16:00:00Z", body.RootElement.GetProperty("clockOutAt").GetString());
        Assert.Equal(480, body.RootElement.GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public async Task ClockOut_WithoutOpenEntry_Returns409()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().PostAsync(ClockOut, content: null, CancellationToken.None);

        await AssertProblemAsync(response, "No hay ningún fichaje abierto");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(59)]
    public async Task ClockOut_TruncatesWorkedMinutes(int extraSeconds)
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        factory.Clock.Advance(new TimeSpan(1, 30, extraSeconds));

        var response = await client.PostAsync(ClockOut, content: null, CancellationToken.None);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(90, body.RootElement.GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public async Task Get_ReturnsItemsAndTotalWorkedMinutes()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        factory.Clock.Advance(TimeSpan.FromHours(1));
        await client.PostAsync(ClockOut, content: null, CancellationToken.None);
        factory.Clock.Advance(TimeSpan.FromMinutes(10));
        await client.PostAsync(ClockIn, content: null, CancellationToken.None);

        var response = await client.GetAsync(TimeEntries, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        // Más reciente primero: la abierta no aporta minutos ni suma al total.
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("durationMinutes").ValueKind);
        Assert.Equal(60, items[1].GetProperty("durationMinutes").GetInt32());
        Assert.Equal(60, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
    }

    [Fact]
    public async Task ClockIn_ThenGet_HasNoLocationAndSameInstant()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        factory.Clock.Advance(TimeSpan.FromMilliseconds(700));

        var created = await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        var listed = await client.GetAsync(TimeEntries, CancellationToken.None);

        Assert.Null(created.Headers.Location);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync(CancellationToken.None));
        using var listedBody = JsonDocument.Parse(await listed.Content.ReadAsStringAsync(CancellationToken.None));
        var createdInstant = createdBody.RootElement.GetProperty("clockInAt").GetString();
        // Sin truncar a segundos, el 201 (en memoria) y el GET (de la base) diferirían en la fracción.
        Assert.Equal("2026-10-12T08:00:00Z", createdInstant);
        Assert.Equal(createdInstant, listedBody.RootElement.GetProperty("items")[0].GetProperty("clockInAt").GetString());
    }

    [Fact]
    public async Task Get_With51Entries_Returns50()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        for (var cycle = 0; cycle < 51; cycle++)
        {
            await client.PostAsync(ClockIn, content: null, CancellationToken.None);
            factory.Clock.Advance(TimeSpan.FromMinutes(1));
            await client.PostAsync(ClockOut, content: null, CancellationToken.None);
            factory.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var response = await client.GetAsync(TimeEntries, CancellationToken.None);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(50, body.RootElement.GetProperty("items").GetArrayLength());
        // Cada fichaje dura un minuto: el total solo cuenta los 50 devueltos.
        Assert.Equal(50, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
    }

    [Fact]
    public async Task Flow_ClockInListClockOutList()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();

        var clockIn = await client.PostAsync(ClockIn, content: null, CancellationToken.None);
        var openList = await ReadListAsync(client);
        factory.Clock.Advance(new TimeSpan(2, 15, 0));
        var clockOut = await client.PostAsync(ClockOut, content: null, CancellationToken.None);
        var closedList = await ReadListAsync(client);

        Assert.Equal(HttpStatusCode.Created, clockIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clockOut.StatusCode);
        Assert.Equal(1, openList.GetProperty("items").GetArrayLength());
        Assert.Equal(0, openList.GetProperty("totalWorkedMinutes").GetInt32());
        var closed = closedList.GetProperty("items")[0];
        Assert.Equal(135, closed.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(closed.GetProperty("durationMinutes").GetInt32(), closedList.GetProperty("totalWorkedMinutes").GetInt32());
    }

    private static async Task<JsonElement> ReadListAsync(HttpClient client)
    {
        var response = await client.GetAsync(TimeEntries, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None)).RootElement.Clone();
    }
}
