using System.Net;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Api.IntegrationTests.TimeEntries;

/// <summary>Dos empleados distintos sobre la misma base: ninguno ve ni cierra los fichajes del otro (BOLA).</summary>
[Collection("Postgres")]
public class TimeEntryIsolationTests(PostgresFixture postgres)
{
    private const string ClockIn = "/api/time-entries/clock-in";
    private const string ClockOut = "/api/time-entries/clock-out";
    private const string TimeEntries = "/api/time-entries";

    [Fact]
    public async Task Request_WithEmployeeIdQueryOrHeader_IsIgnored()
    {
        using var employeeA = new PostgresApiFactory(postgres.ConnectionString);
        var employeeB = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{ClockIn}?employeeId={employeeB}");
        request.Headers.Add("X-Employee-Id", employeeB.ToString());

        var response = await employeeA.CreateClient().SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(await LoadEntriesAsync(employeeA.EmployeeId));
        Assert.Empty(await LoadEntriesAsync(employeeB));
    }

    [Fact]
    public async Task Get_OnlyReturnsOwnEntries()
    {
        using var employeeA = new PostgresApiFactory(postgres.ConnectionString);
        using var employeeB = new PostgresApiFactory(postgres.ConnectionString);
        await employeeA.CreateClient().PostAsync(ClockIn, content: null, CancellationToken.None);

        var ofA = await CountItemsAsync(employeeA);
        var ofB = await CountItemsAsync(employeeB);

        Assert.Equal(1, ofA);
        Assert.Equal(0, ofB);
    }

    [Fact]
    public async Task ClockIn_OfDifferentEmployees_BothSucceed()
    {
        using var employeeA = new PostgresApiFactory(postgres.ConnectionString);
        using var employeeB = new PostgresApiFactory(postgres.ConnectionString);

        var responseA = await employeeA.CreateClient().PostAsync(ClockIn, content: null, CancellationToken.None);
        var responseB = await employeeB.CreateClient().PostAsync(ClockIn, content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, responseB.StatusCode);
    }

    [Fact]
    public async Task ClockOut_CannotCloseOthersEntry()
    {
        using var employeeA = new PostgresApiFactory(postgres.ConnectionString);
        using var employeeB = new PostgresApiFactory(postgres.ConnectionString);
        await employeeA.CreateClient().PostAsync(ClockIn, content: null, CancellationToken.None);

        var response = await employeeB.CreateClient().PostAsync(ClockOut, content: null, CancellationToken.None);

        await TimeEntryEndpointsTests.AssertProblemAsync(response, "No hay ningún fichaje abierto");
        Assert.All(await LoadEntriesAsync(employeeA.EmployeeId), entry => Assert.True(entry.IsOpen));
    }

    [Fact]
    public async Task ClockOut_DoesNotTouchOtherEmployee()
    {
        using var employeeA = new PostgresApiFactory(postgres.ConnectionString);
        using var employeeB = new PostgresApiFactory(postgres.ConnectionString);
        await employeeA.CreateClient().PostAsync(ClockIn, content: null, CancellationToken.None);
        await employeeB.CreateClient().PostAsync(ClockIn, content: null, CancellationToken.None);
        employeeA.Clock.Advance(TimeSpan.FromHours(1));

        var response = await employeeA.CreateClient().PostAsync(ClockOut, content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(await LoadEntriesAsync(employeeA.EmployeeId), entry => Assert.False(entry.IsOpen));
        Assert.All(await LoadEntriesAsync(employeeB.EmployeeId), entry => Assert.True(entry.IsOpen));
    }

    private static async Task<int> CountItemsAsync(PostgresApiFactory factory)
    {
        var response = await factory.CreateClient().GetAsync(TimeEntries, CancellationToken.None);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        return body.RootElement.GetProperty("items").GetArrayLength();
    }

    private async Task<List<TimeEntry>> LoadEntriesAsync(Guid employeeId)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await context.TimeEntries.AsNoTracking().Where(entry => entry.EmployeeId == employeeId).ToListAsync(CancellationToken.None);
    }
}
