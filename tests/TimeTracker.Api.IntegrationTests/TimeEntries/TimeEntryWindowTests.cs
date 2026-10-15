using System.Net;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Api.IntegrationTests.TimeEntries;

[Collection("Postgres")]
public class TimeEntryWindowTests(PostgresFixture postgres)
{
    private const string TimeEntries = "/api/time-entries";

    // Octubre de 2026 en Europe/Madrid: del 30 de septiembre a las 22:00Z al 31 de octubre a las 23:00Z.
    private const string October = "from=2026-09-30T22:00:00Z&to=2026-10-31T23:00:00Z";

    [Fact]
    public async Task Get_WithAMonthWindow_ReturnsOnlyOverlappingEntriesNewestFirst()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var employeeId = factory.EmployeeId;
        await TeamScenario.AddEntryAsync(postgres, employeeId, new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero), 60);
        await TeamScenario.AddEntryAsync(postgres, employeeId, new DateTimeOffset(2026, 9, 30, 21, 30, 0, TimeSpan.Zero), 60); // cruza el inicio
        await TeamScenario.AddEntryAsync(postgres, employeeId, new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.Zero), 120);
        await TeamScenario.AddEntryAsync(postgres, employeeId, new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero), 30); // empieza justo en to

        var response = await factory.CreateClient().GetAsync($"{TimeEntries}?{October}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        var starts = body.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("clockInAt").GetString()).ToList();
        Assert.Equal(["2026-10-05T07:00:00Z", "2026-09-30T21:30:00Z"], starts);
        Assert.Equal(180, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
    }

    [Fact]
    public async Task Get_WithAWindow_KeepsTheStatusOfAnOpenEntryOutsideIt()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        await TeamScenario.AddEntryAsync(postgres, factory.EmployeeId, new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero), null, TimeEntryType.Break);

        var response = await factory.CreateClient().GetAsync($"{TimeEntries}?from=2026-08-31T22:00:00Z&to=2026-09-30T22:00:00Z", CancellationToken.None);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("OnBreak", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_WithoutAWindow_BehavesAsBefore()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        await TeamScenario.AddClosedDailyEntriesAsync(postgres, factory.EmployeeId, new DateTimeOffset(2025, 1, 1, 8, 0, 0, TimeSpan.Zero), 305, 10);

        var response = await factory.CreateClient().GetAsync(TimeEntries, CancellationToken.None);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(300, body.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Get_WithOffsetInstants_NormalizesThemToUtc()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        await TeamScenario.AddEntryAsync(postgres, factory.EmployeeId, new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.Zero), 60);

        var response = await factory.CreateClient().GetAsync($"{TimeEntries}?from=2026-10-01T00:00:00%2B02:00&to=2026-11-01T00:00:00%2B01:00", CancellationToken.None);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(1, body.RootElement.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("from=2026-10-01T00:00:00Z", "Indique el inicio y el fin")]
    [InlineData("to=2026-10-01T00:00:00Z", "Indique el inicio y el fin")]
    [InlineData("from=ayer&to=hoy", "formato ISO 8601")]
    [InlineData("from=2026-10-01&to=2026-10-02T00:00:00Z", "formato ISO 8601")]
    [InlineData("from=2026-10-05T00:00:00Z&to=2026-10-05T00:00:00Z", "posterior al inicio")]
    [InlineData("from=2026-10-05T00:00:00Z&to=2026-09-05T00:00:00Z", "posterior al inicio")]
    [InlineData("from=2026-08-01T00:00:00Z&to=2026-09-10T00:00:00Z", "32 días")]
    [InlineData("from=2026-10-12T08:00:01Z&to=2026-10-13T00:00:00Z", "futuros")]
    [InlineData("from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z", "futuros")]
    public async Task Get_WithAnInvalidWindow_Returns400InSpanish(string query, string expectedDetailPart)
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().GetAsync($"{TimeEntries}?{query}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Solicitud no válida", body.RootElement.GetProperty("title").GetString());
        Assert.Contains(expectedDetailPart, body.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WithAWindowAndNoToken_Returns401()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateAnonymousClient().GetAsync($"{TimeEntries}?{October}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithAWindow_NeverReturnsAnotherEmployeesEntries()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        await TeamScenario.AddEntryAsync(postgres, Guid.NewGuid(), new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.Zero), 60);

        var response = await factory.CreateClient().GetAsync($"{TimeEntries}?{October}", CancellationToken.None);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
    }
}
