using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

[Collection("Postgres")]
public class UserRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AddAsync_ThenFindByEmail_RoundTripsAllProperties()
    {
        var manager = UserSeed.Create("marta", UserRole.Manager);
        var user = UserSeed.Create("ana", UserRole.Manager, manager.Id, "Ana Ejemplo");
        await AddAsync(manager, user);

        var read = await FindByEmailAsync(user.Email);

        Assert.NotNull(read);
        Assert.Equal(user.Id, read.Id);
        Assert.Equal(user.Email, read.Email);
        Assert.Equal("Ana Ejemplo", read.DisplayName);
        Assert.Equal(user.PasswordHash, read.PasswordHash);
        Assert.Equal(UserRole.Manager, read.Role);
        Assert.Equal(manager.Id, read.ManagerId);
        Assert.Equal("Manager", await ScalarAsync($"SELECT role FROM users WHERE id = '{user.Id}'"));
    }

    [Fact]
    public async Task FindByEmailAsync_IsCaseInsensitiveAndReturnsNullWhenMissing()
    {
        var user = UserSeed.Create("luis");
        await AddAsync(user);

        var found = await FindByEmailAsync(user.Email.ToUpperInvariant());

        Assert.Equal(user.Id, found?.Id);
        Assert.Null(await FindByEmailAsync($"nadie.{Guid.NewGuid():N}@example.test"));
    }

    [Fact]
    public async Task ListDirectReportsAsync_ReturnsDirectReportsOrderedByDisplayName()
    {
        var marta = UserSeed.Create("marta", UserRole.Manager);
        var luis = UserSeed.Create("luis", managerId: marta.Id, displayName: "Luis");
        var ana = UserSeed.Create("ana", UserRole.Manager, marta.Id, "Ana");
        var pablo = UserSeed.Create("pablo", displayName: "Pablo");
        var carmen = UserSeed.Create("carmen", managerId: ana.Id, displayName: "Carmen");
        await AddAsync(marta, ana, luis, pablo, carmen);

        var reports = await ListDirectReportsAsync(marta.Id, 50);

        Assert.Equal(["Ana", "Luis"], reports.Select(report => report.DisplayName));
    }

    [Fact]
    public async Task ListDirectReportsAsync_RespectsTheLimit()
    {
        var marta = UserSeed.Create("marta", UserRole.Manager);
        await AddAsync(
            marta,
            UserSeed.Create("c", managerId: marta.Id, displayName: "Carlos"),
            UserSeed.Create("a", managerId: marta.Id, displayName: "Alba"),
            UserSeed.Create("b", managerId: marta.Id, displayName: "Berta"));

        var reports = await ListDirectReportsAsync(marta.Id, 2);

        Assert.Equal(["Alba", "Berta"], reports.Select(report => report.DisplayName));
    }

    [Fact]
    public async Task FindDirectReportAsync_ReturnsOnlyDirectReports()
    {
        var marta = UserSeed.Create("marta", UserRole.Manager);
        var luis = UserSeed.Create("luis", managerId: marta.Id);
        var pablo = UserSeed.Create("pablo");
        await AddAsync(marta, luis, pablo);

        Assert.Equal(luis.Id, (await FindDirectReportAsync(marta.Id, luis.Id))?.Id);
        Assert.Null(await FindDirectReportAsync(marta.Id, pablo.Id));
        Assert.Null(await FindDirectReportAsync(marta.Id, marta.Id));
        Assert.Null(await FindDirectReportAsync(marta.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task UserSeed_UnderParallelUse_NeverCollidesOnEmail()
    {
        var users = Enumerable.Range(0, 20).Select(_ => UserSeed.Create()).ToArray();

        await Task.WhenAll(users.Select(user => AddAsync(user)));

        Assert.Equal(20, users.Select(user => user.Email).Distinct().Count());
    }

    private async Task AddAsync(params UserAccount[] users)
    {
        // Un contexto por usuario y en orden: el responsable debe existir antes que su reporte.
        foreach (var user in users)
        {
            await using var context = postgres.CreateContext(postgres.ConnectionString);
            await new UserRepository(context).AddAsync(user, CancellationToken.None);
        }
    }

    private async Task<UserAccount?> FindByEmailAsync(string email)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await new UserRepository(context).FindByEmailAsync(email, CancellationToken.None);
    }

    private async Task<IReadOnlyList<UserAccount>> ListDirectReportsAsync(Guid managerId, int limit)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await new UserRepository(context).ListDirectReportsAsync(managerId, limit, CancellationToken.None);
    }

    private async Task<UserAccount?> FindDirectReportAsync(Guid managerId, Guid memberId)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString);
        return await new UserRepository(context).FindDirectReportAsync(managerId, memberId, CancellationToken.None);
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(CancellationToken.None);
    }
}
