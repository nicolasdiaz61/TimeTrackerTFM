using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Api.IntegrationTests;

[Collection("Postgres")]
public class ErrorHandlingTests(PostgresFixture postgres)
{
    private const string SensitiveText = "texto-sensible";

    [Fact]
    public async Task UnhandledException_Returns500WithoutDetails()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.Replace(ServiceDescriptor.Scoped<ITimeEntryRepository, ThrowingRepository>())));

        var response = await factory.CreateClient().PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var body = JsonDocument.Parse(text);
        Assert.Equal("Se produjo un error inesperado", body.RootElement.GetProperty("title").GetString());
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain(SensitiveText, text, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
        Assert.DoesNotContain("at TimeTracker", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Conflict_BodyHasNoInternalData()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        var response = await client.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain("Exception", text, StringComparison.Ordinal);
        Assert.DoesNotContain("at TimeTracker", text, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.EmployeeId.ToString(), text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingRepository : ITimeEntryRepository
    {
        public Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken)
            => throw new InvalidOperationException(SensitiveText);

        public Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken)
            => throw new InvalidOperationException(SensitiveText);

        public Task AddAsync(TimeEntry entry, CancellationToken cancellationToken)
            => throw new InvalidOperationException(SensitiveText);

        public Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken)
            => throw new InvalidOperationException(SensitiveText);
    }
}
