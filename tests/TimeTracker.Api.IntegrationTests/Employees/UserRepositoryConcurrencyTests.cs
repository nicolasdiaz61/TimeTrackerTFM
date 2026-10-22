using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// Lecturas seguidas y guardado bajo el token <c>xmin</c> de la fila (DD-2, DD-21): el segundo en guardar tras una lectura común falla
/// y no pisa el cambio del primero.
/// </summary>
[Collection("Postgres")]
public class UserRepositoryConcurrencyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task TwoContextsThatReadTheSameRow_TheSecondSaveFailsAndKeepsTheFirstChange()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var memberId = fixture.A.Employee1.Id;
        await using var first = postgres.CreateContext(postgres.ConnectionString, fixture.A.Id);
        await using var second = postgres.CreateContext(postgres.ConnectionString, fixture.A.Id);
        var firstUser = (await new UserRepository(first).FindForUpdateAsync(memberId, CancellationToken.None))!;
        var secondUser = (await new UserRepository(second).FindForUpdateAsync(memberId, CancellationToken.None))!;

        firstUser.ChangePassword("hash-elegido-por-la-persona");
        await new UserRepository(first).SaveChangesAsync(CancellationToken.None);
        secondUser.RegenerateTemporaryPassword("hash-del-manager");
        await Assert.ThrowsAsync<ConcurrentUserChangeException>(() => new UserRepository(second).SaveChangesAsync(CancellationToken.None));

        Assert.Equal("hash-elegido-por-la-persona|false", await RowAsync(memberId));
    }

    [Fact]
    public async Task ASaveWithoutAnInterveningChange_Succeeds_AndPersistsTheNewHashAndMark()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var memberId = fixture.A.Employee2.Id;
        await using var context = postgres.CreateContext(postgres.ConnectionString, fixture.A.Id);
        var repository = new UserRepository(context);
        var user = (await repository.FindForUpdateAsync(memberId, CancellationToken.None))!;

        user.ChangePassword("hash-nuevo");
        await repository.SaveChangesAsync(CancellationToken.None);

        Assert.Equal("hash-nuevo|false", await RowAsync(memberId));
    }

    [Fact]
    public async Task FindDirectReportForUpdate_OnlyFindsDirectReportsOfTheOwnCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        await using var context = postgres.CreateContext(postgres.ConnectionString, fixture.A.Id);
        var repository = new UserRepository(context);

        var own = await repository.FindDirectReportForUpdateAsync(fixture.A.Manager.Id, fixture.A.Employee1.Id, CancellationToken.None);
        var ofAnotherCompany = await repository.FindDirectReportForUpdateAsync(fixture.A.Manager.Id, fixture.B.Employee1.Id, CancellationToken.None);
        var notDirect = await repository.FindDirectReportForUpdateAsync(fixture.A.Employee1.Id, fixture.A.Employee2.Id, CancellationToken.None);
        var unknown = await repository.FindDirectReportForUpdateAsync(fixture.A.Manager.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(fixture.A.Employee1.Id, own?.Id);
        Assert.Null(ofAnotherCompany);
        Assert.Null(notDirect);
        Assert.Null(unknown);
    }

    [Fact]
    public async Task FindForUpdateAndFindById_AreScopedToTheCompanyOfTheContext()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        await using var context = postgres.CreateContext(postgres.ConnectionString, fixture.A.Id);
        var repository = new UserRepository(context);

        Assert.Equal(fixture.A.Manager.Id, (await repository.FindForUpdateAsync(fixture.A.Manager.Id, CancellationToken.None))?.Id);
        Assert.Equal(fixture.A.Manager.Id, (await repository.FindByIdAsync(fixture.A.Manager.Id, CancellationToken.None))?.Id);
        Assert.Null(await repository.FindForUpdateAsync(fixture.B.Manager.Id, CancellationToken.None));
        Assert.Null(await repository.FindByIdAsync(fixture.B.Manager.Id, CancellationToken.None));
    }

    private async Task<string> RowAsync(Guid userId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand("SELECT password_hash || '|' || must_change_password::text FROM users WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", userId);
        return (string)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }
}
