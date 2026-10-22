using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// <c>ExistsWithEmailDomainAsync</c> acotada por el filtro de compañía: solo ve la del contexto y compara el final del correo de
/// forma literal (un comodín del patrón no debe coincidir con otro carácter).
/// </summary>
[Collection("Postgres")]
public class UserEmailDomainTests(PostgresFixture postgres)
{
    private const string Domain = "@example.test";

    [Fact]
    public async Task Exists_WhenAUserOfTheContextCompanyEndsWithTheDomain_ReturnsTrue()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        await using var context = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);

        Assert.True(await new UserRepository(context).ExistsWithEmailDomainAsync(Domain, CancellationToken.None));
    }

    [Fact]
    public async Task Exists_WhenOnlyAnotherCompanyHasTheDomain_ReturnsFalseForTheContextCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        await UserSeed.InsertRawAsync(fixture.ConnectionString, Guid.NewGuid(), "ana@solo-a.example", companyId: fixture.A.Id);
        await using var companyA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);
        await using var companyB = postgres.CreateContext(fixture.ConnectionString, fixture.B.Id);

        // Control positivo: la compañía A sí tiene un correo de ese dominio; la B no lo ve.
        Assert.True(await new UserRepository(companyA).ExistsWithEmailDomainAsync("@solo-a.example", CancellationToken.None));
        Assert.False(await new UserRepository(companyB).ExistsWithEmailDomainAsync("@solo-a.example", CancellationToken.None));
    }

    [Fact]
    public async Task Exists_ComparesTheEndOfTheEmailLiterally()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        await UserSeed.InsertRawAsync(fixture.ConnectionString, Guid.NewGuid(), "ana@examplextest", companyId: fixture.A.Id);
        await using var context = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);
        var repository = new UserRepository(context);

        Assert.False(await repository.ExistsWithEmailDomainAsync("@example_test", CancellationToken.None));
        Assert.False(await repository.ExistsWithEmailDomainAsync("@example%", CancellationToken.None));
        Assert.True(await repository.ExistsWithEmailDomainAsync("@examplextest", CancellationToken.None));
    }

    [Fact]
    public async Task Exists_WithAnotherDomain_ReturnsFalse()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        await using var context = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);

        Assert.False(await new UserRepository(context).ExistsWithEmailDomainAsync("@empresa-real.example", CancellationToken.None));
    }
}
