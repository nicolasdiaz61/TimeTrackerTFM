using System.Net;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.TimeEntries;

/// <summary>La identidad del empleado sale solo del token (<c>sub</c>), nunca de la configuración ni del cliente (TK-1 a TK-4).</summary>
[Collection("Postgres")]
public class TimeEntryAuthenticationTests(PostgresFixture postgres)
{
    private const string ClockIn = "/api/time-entries/clock-in";
    private const string ClockOut = "/api/time-entries/clock-out";
    private const string TimeEntries = "/api/time-entries";

    [Fact]
    public async Task Identity_ComesFromTheToken()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var luis = factory.CreateClientFor(Guid.NewGuid(), "Employee");
        var ana = factory.CreateClientFor(Guid.NewGuid(), "Employee");

        await luis.PostAsync(ClockIn, content: null, CancellationToken.None);

        Assert.Equal(1, await CountItemsAsync(luis));
        Assert.Equal(0, await CountItemsAsync(ana));
    }

    [Fact]
    public async Task ClockInWithEmployeeIdQuery_IsIgnored()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var (luis, ana) = (Guid.NewGuid(), Guid.NewGuid());

        var response = await factory.CreateClientFor(luis, "Employee")
            .PostAsync($"{ClockIn}?employeeId={ana}", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await CountRowsOfAsync(luis));
        Assert.Equal(0, await CountRowsOfAsync(ana));
    }

    [Fact]
    public async Task ClockInWithXEmployeeIdHeader_IsIgnored()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var (luis, ana) = (Guid.NewGuid(), Guid.NewGuid());
        var request = new HttpRequestMessage(HttpMethod.Post, ClockIn);
        request.Headers.Add("X-Employee-Id", ana.ToString());

        var response = await factory.CreateClientFor(luis, "Employee").SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await CountRowsOfAsync(luis));
        Assert.Equal(0, await CountRowsOfAsync(ana));
    }

    [Fact]
    public async Task TimeEntryEndpoints_WithoutToken_Return401AndCreateNothing()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        var rowsBefore = await CountAllRowsAsync();

        var responses = new[]
        {
            await client.PostAsync(ClockIn, content: null, CancellationToken.None),
            await client.PostAsync(ClockOut, content: null, CancellationToken.None),
            await client.GetAsync(TimeEntries, CancellationToken.None),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.Equal(rowsBefore, await CountAllRowsAsync());
    }

    [Fact]
    public async Task GetTimeEntries_WithExpiredToken_Returns401()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var now = factory.Clock.GetUtcNow();
        var token = TestTokens.Create(Guid.NewGuid(), "Employee", now - TimeSpan.FromHours(1), expires: now - TimeSpan.FromMinutes(2));

        var response = await GetWithAsync(factory, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetTimeEntries_WithInvalidSub_Returns401(string subject)
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var token = TestTokens.Create(Guid.NewGuid(), "Employee", factory.Clock.GetUtcNow(), subject: subject);

        var response = await GetWithAsync(factory, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetWithAsync(PostgresApiFactory factory, string token)
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return await client.GetAsync(TimeEntries, CancellationToken.None);
    }

    private static async Task<int> CountItemsAsync(HttpClient client)
    {
        var response = await client.GetAsync(TimeEntries, CancellationToken.None);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        return body.RootElement.GetProperty("items").GetArrayLength();
    }

    private async Task<int> CountRowsOfAsync(Guid employeeId)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await context.TimeEntries.CountAsync(entry => entry.EmployeeId == employeeId, CancellationToken.None);
    }

    private async Task<int> CountAllRowsAsync()
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await context.TimeEntries.CountAsync(CancellationToken.None);
    }
}
