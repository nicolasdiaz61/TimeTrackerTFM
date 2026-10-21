using System.Net;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Application.Identity;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// Contexto de compañía transitorio de la PR 1 (DD-7): una sola compañía, la de por defecto, hasta que la PR 2 la
/// tome del token. El comportamiento visible no cambia, pero las altas ya escriben <c>company_id</c>.
/// </summary>
[Collection("Postgres")]
public class FixedCompanyContextTests(PostgresFixture postgres)
{
    [Fact]
    public void FixedCompanyContext_AlwaysReturnsTheDefaultCompany()
    {
        ICompanyContext context = new FixedCompanyContext();

        Assert.Equal(Company.DefaultId, context.CompanyId);
    }

    [Fact]
    public void Host_RegistersTheFixedContextAsTheCompanyContext()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ICompanyContext>();

        Assert.IsType<FixedCompanyContext>(context);
        Assert.Equal(Company.DefaultId, context.CompanyId);
    }

    [Fact]
    public async Task ClockIn_WritesTheDefaultCompanyIntoTheRow()
    {
        using var factory = new PostgresApiFactory(postgres.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand($"SELECT company_id::text FROM time_entries WHERE employee_id = '{factory.EmployeeId}'", connection);
        Assert.Equal(Company.DefaultId.ToString(), await command.ExecuteScalarAsync(CancellationToken.None));
    }
}
