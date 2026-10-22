using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// TC-13 (DD-9, DD-11): el correo es único global y su búsqueda (sitio 1 de la lista cerrada) atraviesa el filtro; el resto de
/// búsquedas de usuarios quedan acotadas a la compañía del contexto.
/// </summary>
[Collection("Postgres")]
public class UserLookupTests(PostgresFixture postgres)
{
    [Fact]
    public async Task SignUpInCompanyA_OfAnEmailOfCompanyB_ViolatesTheGlobalUniqueIndex()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var duplicate = UserAccount.Create(Guid.NewGuid(), fixture.A.Id, fixture.B.Employee1.Email, "Repetida", UserSeed.PasswordHash, UserRole.Employee, managerId: null);
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => new UserRepository(contextA).AddAsync(duplicate, CancellationToken.None));

        var postgresError = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresError.SqlState);
        Assert.Equal("ux_users_email", postgresError.ConstraintName);
    }

    [Fact]
    public async Task FindByEmail_FindsThePersonInAnyCompany_WhateverTheContextCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);

        foreach (var companyOfContext in new Guid?[] { fixture.A.Id, fixture.B.Id, Guid.Empty })
        {
            await using var context = new TimeTrackerDbContext(
                new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(fixture.ConnectionString).Options,
                new TestCompanyContext(companyOfContext));
            var repository = new UserRepository(context);

            var ofA = await repository.FindByEmailAsync(fixture.A.Employee1.Email.ToUpperInvariant(), CancellationToken.None);
            var ofB = await repository.FindByEmailAsync(fixture.B.Employee1.Email, CancellationToken.None);

            Assert.Equal((fixture.A.Employee1.Id, fixture.A.Id), (ofA?.Id, ofA?.CompanyId));
            Assert.Equal((fixture.B.Employee1.Id, fixture.B.Id), (ofB?.Id, ofB?.CompanyId));
        }

        await using var withoutCompany = new TimeTrackerDbContext(
            new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(fixture.ConnectionString).Options,
            new TestCompanyContext(null));
        Assert.Null(await new UserRepository(withoutCompany).FindByEmailAsync($"nadie.{Guid.NewGuid():N}@example.test", CancellationToken.None));
        Assert.NotNull(await new UserRepository(withoutCompany).FindByEmailAsync(fixture.A.Manager.Email, CancellationToken.None));
    }

    [Fact]
    public async Task DirectReportLookups_AreBoundedToTheContextCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);
        var repository = new UserRepository(contextA);

        var ownReports = await repository.ListDirectReportsAsync(fixture.A.Manager.Id, 50, CancellationToken.None);
        var foreignReports = await repository.ListDirectReportsAsync(fixture.B.Manager.Id, 50, CancellationToken.None);

        Assert.Equal(new[] { fixture.A.Employee1.Id, fixture.A.Employee2.Id }.Order(), ownReports.Select(report => report.Id).Order());
        Assert.Empty(foreignReports);
        Assert.Equal(fixture.A.Employee1.Id, (await repository.FindDirectReportAsync(fixture.A.Manager.Id, fixture.A.Employee1.Id, CancellationToken.None))?.Id);
        Assert.Null(await repository.FindDirectReportAsync(fixture.B.Manager.Id, fixture.B.Employee1.Id, CancellationToken.None));
        Assert.Null(await repository.FindDirectReportAsync(fixture.A.Manager.Id, fixture.B.Employee1.Id, CancellationToken.None));
    }

    [Fact]
    public async Task FindDisplayName_ResolvesOnlyPeopleOfTheContextCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);
        var repository = new UserRepository(contextA);

        Assert.Equal("Usuario de prueba", await repository.FindDisplayNameAsync(fixture.A.Employee2.Id, CancellationToken.None));
        Assert.Null(await repository.FindDisplayNameAsync(fixture.B.Employee2.Id, CancellationToken.None));
    }
}
