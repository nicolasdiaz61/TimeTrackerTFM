using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// Spike V-2 y V-9 contra EF Core 10.0.12 (DD-8, DD-10): cómo se comporta el filtro global construido por convención.
/// Los resultados se anotan en design.md.
/// </summary>
[Collection("Postgres")]
public class QueryFilterSpikeTests(PostgresFixture postgres)
{
    [Fact]
    public void NamedQueryFilters_ExistInEfCore10_ButTheChangeAdoptsOnlyOneUnnamedFilter()
    {
        // V-2 (a): solo se registra el resultado; este cambio no depende de ellos (DD-10).
        var offersNamedFilters = typeof(EntityTypeBuilder<>).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(method => method.Name == "HasQueryFilter" && method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == typeof(string));
        Assert.True(offersNamedFilters, "EF Core 10.0.12 ofrece HasQueryFilter(nombre, expresión).");

        using var context = postgres.CreateContext(postgres.ConnectionString);
        var userType = context.Model.FindEntityType(typeof(UserAccount))!;
        Assert.Single(userType.GetDeclaredQueryFilters());
        Assert.Null(userType.GetDeclaredQueryFilters().Single().Key);
    }

    [Fact]
    public async Task TwoContextsWithDifferentCompanies_InTheSameProcess_DoNotContaminateEachOther()
    {
        var (companyA, companyB, userA, userB) = await TwoCompaniesWithOneUserEachAsync();
        await using var contextA = Context(new TestCompanyContext(companyA));
        await using var contextB = Context(new TestCompanyContext(companyB));

        // Se repite para que la segunda ejecución use la consulta compilada en caché.
        for (var round = 0; round < 2; round++)
        {
            Assert.Equal([userA], await contextA.Users.AsNoTracking().Where(u => u.Id == userA || u.Id == userB).Select(u => u.Id).ToListAsync());
            Assert.Equal([userB], await contextB.Users.AsNoTracking().Where(u => u.Id == userA || u.Id == userB).Select(u => u.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task TheFilterReadsTheCompanyOfTheContextOnEachExecution_NotWhenTheQueryIsCompiled()
    {
        var (companyA, companyB, userA, userB) = await TwoCompaniesWithOneUserEachAsync();
        var current = new TestCompanyContext(companyA);
        await using var context = Context(current);

        Assert.Equal([userA], await context.Users.AsNoTracking().Where(u => u.Id == userA || u.Id == userB).Select(u => u.Id).ToListAsync());
        current.CompanyId = companyB;
        Assert.Equal([userB], await context.Users.AsNoTracking().Where(u => u.Id == userA || u.Id == userB).Select(u => u.Id).ToListAsync());
    }

    [Fact]
    public async Task FindAsync_AppliesTheFilterOnTheDatabaseLookup_InEfCore10()
    {
        // V-9: la hipótesis del diseño era que Find no aplica el filtro; en 10.0.12 la búsqueda en base de datos sí lo aplica.
        // Se mantiene igualmente la prohibición de Find/FindAsync en src/ (DD-9): una entidad ya rastreada se devuelve sin consulta.
        var (companyA, _, userA, userB) = await TwoCompaniesWithOneUserEachAsync();
        await using var contextA = Context(new TestCompanyContext(companyA));

        Assert.Equal(userA, (await contextA.Users.FindAsync(userA))?.Id);
        Assert.Null(await contextA.Users.FindAsync(userB));
    }

    [Fact]
    public async Task ExecuteUpdateAndExecuteDelete_RespectTheFilter()
    {
        var (companyA, _, _, userB) = await TwoCompaniesWithOneUserEachAsync();
        await using var contextA = Context(new TestCompanyContext(companyA));

        var updated = await contextA.Users.Where(u => u.Id == userB).ExecuteUpdateAsync(s => s.SetProperty(u => u.DisplayName, "cambiado"));
        var deleted = await contextA.Users.Where(u => u.Id == userB).ExecuteDeleteAsync();

        Assert.Equal(0, updated);
        Assert.Equal(0, deleted);
        Assert.Equal("Usuario de prueba", await ScalarAsync($"SELECT display_name FROM users WHERE id = '{userB}'"));
    }

    private TimeTrackerDbContext Context(TestCompanyContext company)
        => new(new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(postgres.ConnectionString).Options, company);

    private async Task<(Guid CompanyA, Guid CompanyB, Guid UserA, Guid UserB)> TwoCompaniesWithOneUserEachAsync()
    {
        var companyA = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var companyB = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, userA, $"a.{userA:N}@example.test", companyId: companyA);
        await UserSeed.InsertRawAsync(postgres.ConnectionString, userB, $"b.{userB:N}@example.test", companyId: companyB);
        return (companyA, companyB, userA, userB);
    }

    private async Task<string?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (string?)await command.ExecuteScalarAsync();
    }
}
