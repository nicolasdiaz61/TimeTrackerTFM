using System.Net;
using System.Text.Json;

using TimeTracker.Api.IntegrationTests.Postgres;

namespace TimeTracker.Api.IntegrationTests.TimeEntries;

[Collection("Postgres")]
public class TimeEntryConcurrencyTests(PostgresFixture postgres)
{
    private const int Parallelism = 10;

    [Fact]
    public async Task ClockIn_TenParallel_OneCreatedNineConflict()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, Parallelism)
            .Select(_ => client.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(Parallelism - 1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        var listed = await client.GetAsync("/api/time-entries", CancellationToken.None);
        using var body = JsonDocument.Parse(await listed.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(1, body.RootElement.GetProperty("items").GetArrayLength());
    }
}
