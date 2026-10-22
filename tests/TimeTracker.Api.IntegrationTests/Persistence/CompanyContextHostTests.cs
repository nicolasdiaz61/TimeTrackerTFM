using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Api.Security;
using TimeTracker.Application.Identity;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>El contexto de compañía de la petición sale del token (DD-7): sustituye al contexto fijo de la PR 1.</summary>
[Collection("Postgres")]
public class CompanyContextHostTests(PostgresFixture postgres)
{
    [Fact]
    public void Host_RegistersTheAmbientContextAsBothPortsOfTheSameScopedInstance()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ICompanyContext>();
        var setter = scope.ServiceProvider.GetRequiredService<ICompanyContextSetter>();

        Assert.IsType<AmbientCompanyContext>(context);
        Assert.Same(context, setter);
        Assert.Null(context.CompanyId);
    }

    [Fact]
    public async Task ClockIn_WritesTheCompanyOfTheTokenNotTheDefaultOne()
    {
        var companyId = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var employeeId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, employeeId, $"ana.{employeeId:N}@example.test", companyId: companyId);
        using var factory = new PostgresApiFactory(postgres.ConnectionString, employeeId);
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokens.Create(employeeId, "Employee", factory.Clock.GetUtcNow(), companyId: companyId.ToString()));

        var response = await client.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(companyId.ToString(), await CompanyOfEntryAsync(employeeId));
    }

    [Fact]
    public async Task ClockIn_WithTheDefaultCompanyToken_WritesTheDefaultCompany()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Company.DefaultId.ToString(), await CompanyOfEntryAsync(factory.EmployeeId));
    }

    private async Task<string?> CompanyOfEntryAsync(Guid employeeId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand($"SELECT company_id::text FROM time_entries WHERE employee_id = '{employeeId}'", connection);
        return (string?)await command.ExecuteScalarAsync(CancellationToken.None);
    }
}
