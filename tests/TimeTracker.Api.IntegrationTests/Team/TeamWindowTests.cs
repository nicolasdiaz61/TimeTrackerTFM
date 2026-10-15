using System.Net;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Security;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Api.IntegrationTests.Team;

[Collection("Postgres")]
public class TeamWindowTests(PostgresFixture postgres)
{
    private const string October = "from=2026-09-30T22:00:00Z&to=2026-10-31T23:00:00Z";

    private static string EntriesOf(Guid memberId) => $"/api/team/members/{memberId}/time-entries";

    [Fact]
    public async Task MemberEntries_WithAMonthWindow_ReturnsOnlyThatMembersEntriesOfThatMonth()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.Zero), 90);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, new DateTimeOffset(2026, 9, 5, 7, 0, 0, TimeSpan.Zero), 45);
        await TeamScenario.AddEntryAsync(postgres, scenario.Ana.Id, new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.Zero), 30);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, new DateTimeOffset(2026, 10, 12, 7, 0, 0, TimeSpan.Zero), null, TimeEntryType.MealBreak);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager").GetAsync($"{EntriesOf(scenario.Luis.Id)}?{October}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(2, body.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(90, body.RootElement.GetProperty("totalWorkedMinutes").GetInt32());
        Assert.Equal("OnMealBreak", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MemberEntries_StatusIsTheMembersOpenEntryEvenOutsideTheWindow()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        await TeamScenario.AddEntryAsync(postgres, scenario.Luis.Id, new DateTimeOffset(2026, 10, 12, 7, 0, 0, TimeSpan.Zero), null);

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager")
            .GetAsync($"{EntriesOf(scenario.Luis.Id)}?from=2026-08-31T22:00:00Z&to=2026-09-30T22:00:00Z", CancellationToken.None);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Working", body.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MemberEntries_WithAnInvalidWindow_Returns400_ForOwnAndForeignMembers(bool ownMember)
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var memberId = ownMember ? scenario.Luis.Id : scenario.Carmen.Id;

        var response = await factory.CreateClientFor(scenario.Marta.Id, "Manager")
            .GetAsync($"{EntriesOf(memberId)}?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Contains("futuros", body.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("unknown")]
    [InlineData("self")]
    public async Task MemberEntries_WithAValidWindow_Returns404IdenticalToTheWindowlessCase(string kind)
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var memberId = kind switch
        {
            "foreign" => scenario.Carmen.Id,
            "self" => scenario.Marta.Id,
            _ => Guid.NewGuid(),
        };
        var client = factory.CreateClientFor(scenario.Marta.Id, "Manager");

        var plain = await client.GetAsync(EntriesOf(memberId), CancellationToken.None);
        var windowed = await client.GetAsync($"{EntriesOf(memberId)}?{October}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, plain.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, windowed.StatusCode);
        (await AccessErrorsTests.AssertProblemAsync(windowed, 404, "Recurso no encontrado")).Dispose();
    }

    [Fact]
    public async Task MemberEntries_AsEmployeeWithAWindow_StillReturns403()
    {
        var scenario = await TeamScenario.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClientFor(scenario.Ana.Id, "Employee").GetAsync($"{EntriesOf(scenario.Luis.Id)}?{October}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
