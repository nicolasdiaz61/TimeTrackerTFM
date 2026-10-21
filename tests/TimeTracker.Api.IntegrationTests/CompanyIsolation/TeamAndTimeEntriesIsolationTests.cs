using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;

using static TimeTracker.Api.IntegrationTests.Support.TwoCompanyFixture;

namespace TimeTracker.Api.IntegrationTests.CompanyIsolation;

/// <summary>
/// TC-1 (DD-11): equipo y fichajes acotados a la compañía. Un identificador de B desde A es indistinguible de uno inexistente, y la
/// compañía de un fichaje nuevo sale solo del token.
/// </summary>
[Collection("Postgres")]
public class TeamAndTimeEntriesIsolationTests(PostgresFixture postgres)
{
    private const string Members = "/api/team/members";
    private const string TimeEntries = "/api/time-entries";

    [Fact]
    public async Task TeamMembers_ListsOnlyTheDirectReportsOfTheOwnCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);

        foreach (var (own, other) in new[] { (fixture.A, fixture.B), (fixture.B, fixture.A) })
        {
            var text = await ClientFor(factory, own, own.Manager).GetStringAsync(Members, CancellationToken.None);

            using var body = JsonDocument.Parse(text);
            Assert.Equal(
                new[] { own.Employee1.Id, own.Employee2.Id }.Order().ToList(),
                body.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Order().ToList());
            foreach (var foreign in other.All)
            {
                Assert.DoesNotContain(foreign.Id.ToString(), text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task MemberTimeEntries_OfAnEmployeeOfTheOwnCompany_AreReturned_ButNeverOfAnotherCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var managerA = ClientFor(factory, fixture.A, fixture.A.Manager);

        var own = await managerA.GetAsync($"{Members}/{fixture.A.Employee1.Id}/time-entries", CancellationToken.None);
        var foreign = await managerA.GetAsync($"{Members}/{fixture.B.Employee1.Id}/time-entries", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var ownBody = JsonDocument.Parse(await own.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Single(ownBody.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task AnIdentifierOfAnotherCompany_GivesTheSame404AsAnUnknownGuid()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var managerA = ClientFor(factory, fixture.A, fixture.A.Manager);

        var ofB = await Snapshot404Async(managerA, fixture.B.Employee1.Id);
        var managerOfB = await Snapshot404Async(managerA, fixture.B.Manager.Id);
        var unknown = await Snapshot404Async(managerA, Guid.NewGuid());

        Assert.Equal(unknown, ofB);
        Assert.Equal(unknown, managerOfB);
        Assert.Contains("\"status\":404", unknown.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWindowedQueryFor_AMemberOfAnotherCompany_GivesTheSameAnswerAsForAnUnknownOne()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var managerA = ClientFor(factory, fixture.A, fixture.A.Manager);
        const string futureWindow = "?from=2999-01-01&to=2999-01-02";

        var foreign = await managerA.GetAsync($"{Members}/{fixture.B.Employee1.Id}/time-entries{futureWindow}", CancellationToken.None);
        var unknown = await managerA.GetAsync($"{Members}/{Guid.NewGuid()}/time-entries{futureWindow}", CancellationToken.None);

        Assert.Equal(unknown.StatusCode, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [Fact]
    public async Task OwnTimeEntries_ContainOnlyTheEntriesOfTheEmployeeOfTheTokenCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);

        foreach (var company in new[] { fixture.A, fixture.B })
        {
            using var body = JsonDocument.Parse(await ClientFor(factory, company, company.Employee1).GetStringAsync(TimeEntries, CancellationToken.None));

            Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        }

        // Una persona cuyo id existe en B pero con un token de A (cid de A) no ve nada: el filtro usa la compañía del token.
        var crossed = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.B.Employee1);
        using var crossedBody = JsonDocument.Parse(await crossed.GetStringAsync(TimeEntries, CancellationToken.None));
        Assert.Empty(crossedBody.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ATokenOfCompanyA_WithTheIdOfAManagerOfB_SeesNothingOfB()
    {
        // La identidad de B con la compañía de A (un token inconsistente): solo el filtro, no el identificador, impide ver a B.
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var crossed = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.B.Manager);

        using var members = JsonDocument.Parse(await crossed.GetStringAsync(Members, CancellationToken.None));
        var entries = await crossed.GetAsync($"{Members}/{fixture.B.Employee1.Id}/time-entries", CancellationToken.None);

        Assert.Empty(members.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, entries.StatusCode);
    }

    [Fact]
    public async Task ClockIn_IgnoresAnyCompanyIdentifierSentByTheClient_AndCreatesTheEntryInTheTokenCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var employeeA = ClientFor(factory, fixture.A, fixture.A.Employee1);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TimeEntries}/clock-in?companyId={fixture.B.Id}")
        {
            Content = JsonContent.Create(new JsonObject { ["companyId"] = fixture.B.Id.ToString() }),
        };
        request.Headers.Add("X-Company-Id", fixture.B.Id.ToString());

        var response = await employeeA.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            [fixture.A.Id.ToString()],
            await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT company_id::text FROM time_entries WHERE employee_id = '{fixture.A.Employee1.Id}' AND clock_out_at IS NULL"));
        Assert.Equal(["0"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT count(*)::text FROM time_entries WHERE employee_id = '{fixture.A.Employee1.Id}' AND company_id = '{fixture.B.Id}'"));
        Assert.DoesNotContain(fixture.B.Id.ToString(), await response.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClockIn_AsAPersonWhoseCompanyIsB_CreatesTheEntryInB()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);

        var response = await ClientFor(factory, fixture.B, fixture.B.Employee2).PostAsync($"{TimeEntries}/clock-in?companyId={fixture.A.Id}", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            [fixture.B.Id.ToString()],
            await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT company_id::text FROM time_entries WHERE employee_id = '{fixture.B.Employee2.Id}' AND clock_out_at IS NULL"));
    }

    private static async Task<Snapshot> Snapshot404Async(HttpClient client, Guid memberId)
    {
        var response = await client.GetAsync($"{Members}/{memberId}/time-entries", CancellationToken.None);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None))!.AsObject();
        body.Remove("traceId");
        var headers = new[] { "Cache-Control", "X-Content-Type-Options" }
            .Select(name => $"{name}={(response.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : string.Empty)}");
        return new Snapshot((int)response.StatusCode, response.Content.Headers.ContentType?.ToString(), string.Join(';', headers), body.ToJsonString());
    }

    private sealed record Snapshot(int Status, string? ContentType, string Headers, string Body);
}
