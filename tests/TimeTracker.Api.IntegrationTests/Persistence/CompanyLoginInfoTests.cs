using Microsoft.Extensions.Time.Testing;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>Lectura de nombre y estado por clave primaria de <c>companies</c> (tabla sin filtro de compañía, DD-8).</summary>
[Collection("Postgres")]
public class CompanyLoginInfoTests(PostgresFixture postgres)
{
    [Fact]
    public async Task FindLoginInfoAsync_ForTheDefaultCompany_ReturnsDemoActiveWithThePlatformAssistant()
    {
        var info = await FindAsync(Company.DefaultId);

        Assert.NotNull(info);
        Assert.Equal(("Demo", CompanyStatus.Active, true), (info.Name, info.Status, info.UsesPlatformAssistant));
    }

    [Fact]
    public async Task FindLoginInfoAsync_ForANewCompany_ReturnsItsNameAndNoPlatformAssistant()
    {
        var companyId = await CompanySeed.InsertAsync(postgres.ConnectionString, "Acme Norte (repositorio)");

        var info = await FindAsync(companyId);

        Assert.NotNull(info);
        Assert.Equal(("Acme Norte (repositorio)", CompanyStatus.Active, false), (info.Name, info.Status, info.UsesPlatformAssistant));
    }

    [Fact]
    public async Task FindLoginInfoAsync_ForASuspendedCompany_ReportsTheStatus()
    {
        var companyId = await CompanySeed.InsertAsync(postgres.ConnectionString);
        await using (var connection = new NpgsqlConnection(postgres.ConnectionString))
        {
            await connection.OpenAsync(CancellationToken.None);
            await using var command = new NpgsqlCommand("UPDATE companies SET status = 'Suspended' WHERE id = @id", connection);
            command.Parameters.AddWithValue("id", companyId);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var info = await FindAsync(companyId);

        Assert.Equal(CompanyStatus.Suspended, info?.Status);
    }

    [Fact]
    public async Task FindLoginInfoAsync_ForAnUnknownCompany_ReturnsNull()
        => Assert.Null(await FindAsync(Guid.NewGuid()));

    private async Task<CompanyLoginInfo?> FindAsync(Guid companyId)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await new CompanyRepository(context, new FakeTimeProvider()).FindLoginInfoAsync(companyId, CancellationToken.None);
    }
}
