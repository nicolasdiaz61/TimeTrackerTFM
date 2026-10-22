using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>Tabla <c>users</c> tras la migración B: columnas, restricciones y claves foráneas compuestas.</summary>
[Collection("Postgres")]
public class UsersTableConstraintsTests(PostgresFixture postgres)
{
    private const string ManagerConstraint = "fk_users_users_company_id_manager_id";

    private static string NewEmail() => $"usuario.{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task Users_Table_HasEightColumnsAndOnlyManagerIdIsNullable()
    {
        var columns = await MigrationDatabase.QueryAsync(
            postgres.ConnectionString,
            "SELECT column_name || ':' || is_nullable FROM information_schema.columns WHERE table_name = 'users' ORDER BY column_name");

        Assert.Equal(
            ["company_id:NO", "display_name:NO", "email:NO", "id:NO", "manager_id:YES", "must_change_password:NO", "password_hash:NO", "role:NO"],
            columns);
        Assert.Equal(
            ["false"],
            await MigrationDatabase.QueryAsync(postgres.ConnectionString, "SELECT column_default FROM information_schema.columns WHERE table_name = 'users' AND column_name = 'must_change_password'"));
    }

    [Fact]
    public async Task DuplicateEmail_IsRejectedWithUniqueViolationEvenInAnotherCompany()
    {
        var email = NewEmail();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), email);

        var sameCompany = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), email));
        var otherCompany = await Assert.ThrowsAsync<PostgresException>(
            async () => await UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), email, companyId: await CompanySeed.InsertAsync(postgres.ConnectionString)));

        Assert.All([sameCompany, otherCompany], error =>
        {
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            Assert.Equal("ux_users_email", error.ConstraintName);
        });
    }

    [Fact]
    public async Task UnknownManagerId_IsRejectedWithForeignKeyViolation()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), NewEmail(), managerId: Guid.NewGuid()));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(ManagerConstraint, error.ConstraintName);
    }

    [Fact]
    public async Task ManagerOfAnotherCompany_IsRejectedWithForeignKeyViolation()
    {
        var managerId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, managerId, NewEmail(), "Manager", companyId: await CompanySeed.InsertAsync(postgres.ConnectionString));

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), NewEmail(), managerId: managerId));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(ManagerConstraint, error.ConstraintName);
    }

    [Fact]
    public async Task DeletingAManagerWithReports_IsRejectedAndKeepsTheReportIntact()
    {
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, managerId, NewEmail(), "Manager");
        await UserSeed.InsertRawAsync(postgres.ConnectionString, reportId, NewEmail(), managerId: managerId);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => MigrationDatabase.ExecuteAsync(postgres.ConnectionString, $"DELETE FROM users WHERE id = '{managerId}'"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal([managerId.ToString()], await MigrationDatabase.QueryAsync(postgres.ConnectionString, $"SELECT manager_id::text FROM users WHERE id = '{reportId}'"));
    }

    [Fact]
    public async Task TheCompositeManagerKey_IsNoActionAndNotDeferrable()
    {
        var rows = await MigrationDatabase.QueryAsync(
            postgres.ConnectionString,
            $"SELECT confdeltype::text || '|' || confupdtype::text || '|' || confmatchtype::text || '|' || condeferrable::text FROM pg_constraint WHERE conname = '{ManagerConstraint}'");

        Assert.Equal(["a|a|s|false"], rows);
    }

    [Fact]
    public async Task ManagerEqualToId_IsRejectedByCheck()
    {
        var id = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, id, NewEmail(), managerId: id));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_users_manager_not_self", error.ConstraintName);
    }

    [Fact]
    public async Task UnknownRole_IsRejectedByCheck()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), NewEmail(), "Admin"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_users_role", error.ConstraintName);
    }

    [Fact]
    public async Task EmailWithUppercase_IsRejectedByCheck()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), "Luis.Empleado@example.test"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_users_email_lowercase", error.ConstraintName);
    }

    [Fact]
    public async Task ARowWithoutCompanyId_IsRejectedAsNotNull()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => MigrationDatabase.ExecuteAsync(postgres.ConnectionString, $"INSERT INTO users (id, email, display_name, password_hash, role) VALUES (gen_random_uuid(), '{NewEmail()}', 'Sin compañía', 'hash', 'Employee')"));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, error.SqlState);
        Assert.Equal("company_id", error.ColumnName);
    }

    [Fact]
    public async Task AManagerWithAPendingPasswordChange_RoundTripsThroughTheContext()
    {
        var manager = UserAccount.Create(Guid.NewGuid(), Company.DefaultId, NewEmail(), "Marta Directora", UserSeed.PasswordHash, UserRole.Manager, managerId: null, mustChangePassword: true);
        await using (var writer = postgres.CreateContext(postgres.ConnectionString))
        {
            await UserSeed.AddAsync(writer, manager);
        }

        await using var reader = postgres.CreateContext(postgres.ConnectionString);
        var read = await reader.Users.AsNoTracking().SingleAsync(user => user.Id == manager.Id, CancellationToken.None);

        Assert.Equal((manager.Id, Company.DefaultId, manager.Email, "Marta Directora", UserRole.Manager, (Guid?)null, true), (read.Id, read.CompanyId, read.Email, read.DisplayName, read.Role, read.ManagerId, read.MustChangePassword));
    }
}
