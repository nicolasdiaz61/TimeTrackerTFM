using System.Net;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;

using static TimeTracker.Api.IntegrationTests.Support.TwoCompanyFixture;

namespace TimeTracker.Api.IntegrationTests.CompanyIsolation;

/// <summary>
/// Matriz de los endpoints existentes (fichajes, equipo y sesión) con tokens de A y de B: ninguno devuelve datos de la otra
/// compañía ni modifica sus filas, también cuando se le pregunta por identificadores ajenos.
/// </summary>
[Collection("Postgres")]
public class ExistingEndpointsMatrixTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task NoEndpointReturnsOrChangesDataOfTheOtherCompany(bool actorIsA, bool actorIsManager)
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new PostgresApiFactory(fixture.ConnectionString);
        var (own, other) = actorIsA ? (fixture.A, fixture.B) : (fixture.B, fixture.A);
        var actor = actorIsManager ? own.Manager : own.Employee1;
        var client = ClientFor(factory, own, actor);
        var foreignTexts = await ForeignTextsAsync(fixture.ConnectionString, other);
        var before = await SnapshotAsync(fixture.ConnectionString, other.Id);

        var responses = new List<(string Request, HttpStatusCode Status, string Body)>();
        foreach (var path in ReadPaths(other))
        {
            responses.Add(await SendAsync(client, HttpMethod.Get, path));
        }

        foreach (var action in new[] { "clock-in", "start-break", "end-break", "start-meal-break", "end-break", "clock-out" })
        {
            responses.Add(await SendAsync(client, HttpMethod.Post, $"/api/time-entries/{action}?companyId={other.Id}"));
        }

        foreach (var response in responses)
        {
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.Status);
            foreach (var foreign in foreignTexts)
            {
                Assert.DoesNotContain(foreign, response.Body, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Control: la matriz sí recorrió endpoints que responden con datos propios (no es una matriz vacía).
        Assert.Contains(responses, response => response.Request.EndsWith("/api/time-entries", StringComparison.Ordinal) && response.Status == HttpStatusCode.OK);
        Assert.Contains(responses, response => response.Request.Contains("clock-in", StringComparison.Ordinal) && response.Status == HttpStatusCode.Created);
        Assert.Equal(before, await SnapshotAsync(fixture.ConnectionString, other.Id));
    }

    private static IEnumerable<string> ReadPaths(CompanyMembers other)
    {
        yield return "/api/time-entries";
        yield return "/api/auth/me";
        yield return "/api/team/members";
        foreach (var member in other.All)
        {
            yield return $"/api/team/members/{member.Id}/time-entries";
        }
    }

    private static async Task<(string Request, HttpStatusCode Status, string Body)> SendAsync(HttpClient client, HttpMethod method, string path)
    {
        using var response = await client.SendAsync(new HttpRequestMessage(method, path), CancellationToken.None);
        return ($"{method} {path}", response.StatusCode, await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    // Todo lo que identifica a la otra compañía: su id y nombre, y los identificadores y correos de sus personas y fichajes.
    private static async Task<List<string>> ForeignTextsAsync(string connectionString, CompanyMembers other)
    {
        var texts = new List<string> { other.Id.ToString(), other.Name };
        texts.AddRange(other.All.SelectMany(member => new[] { member.Id.ToString(), member.Email }));
        texts.AddRange(await MigrationDatabase.QueryAsync(connectionString, $"SELECT id::text FROM time_entries WHERE company_id = '{other.Id}'"));
        return texts;
    }

    private static async Task<string> SnapshotAsync(string connectionString, Guid companyId)
        => string.Join(
            '|',
            await MigrationDatabase.QueryAsync(
                connectionString,
                $"SELECT id || ',' || employee_id || ',' || clock_in_at || ',' || coalesce(clock_out_at::text, '-') || ',' || type FROM time_entries WHERE company_id = '{companyId}' ORDER BY id"));
}
