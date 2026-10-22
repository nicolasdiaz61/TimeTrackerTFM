using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// Claves foráneas compuestas (DD-2): un responsable y el empleado de un fichaje solo pueden ser de la misma compañía
/// que la fila que los referencia, y borrar un usuario con fichajes se rechaza (la baja de usuarios queda pendiente, ADR 0020).
/// </summary>
[Collection("Postgres")]
public class CompositeForeignKeyTests(PostgresFixture postgres)
{
    private const string ManagerConstraint = "fk_users_users_company_id_manager_id";
    private const string EmployeeConstraint = "fk_time_entries_users_company_id_employee_id";

    private static string NewEmail() => $"usuario.{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task AUserWhoseManagerBelongsToAnotherCompany_IsRejectedWithForeignKeyViolation()
    {
        var otherCompany = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var manager = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, manager, NewEmail(), "Manager", companyId: otherCompany);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), NewEmail(), managerId: manager, companyId: Company.DefaultId));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(ManagerConstraint, error.ConstraintName);
    }

    [Fact]
    public async Task AUserWhoseManagerBelongsToTheSameCompany_IsAccepted()
    {
        var company = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var manager = Guid.NewGuid();
        var report = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, manager, NewEmail(), "Manager", companyId: company);

        await UserSeed.InsertRawAsync(postgres.ConnectionString, report, NewEmail(), managerId: manager, companyId: company);

        Assert.Equal([manager.ToString()], await MigrationDatabase.QueryAsync(postgres.ConnectionString, $"SELECT manager_id::text FROM users WHERE id = '{report}'"));
    }

    [Fact]
    public async Task AUserWithoutManager_IsAcceptedBecauseTheCompositeKeyIsMatchSimple()
    {
        var company = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var id = Guid.NewGuid();

        await UserSeed.InsertRawAsync(postgres.ConnectionString, id, NewEmail(), companyId: company);

        Assert.Equal([$"null|{company}"], await MigrationDatabase.QueryAsync(postgres.ConnectionString, $"SELECT COALESCE(manager_id::text, 'null') || '|' || company_id FROM users WHERE id = '{id}'"));
    }

    [Fact]
    public async Task ATimeEntryWhoseCompanyDiffersFromThatOfItsEmployee_IsRejectedWithForeignKeyViolation()
    {
        var otherCompany = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var employee = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, employee, NewEmail(), companyId: Company.DefaultId);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => CompanySeed.InsertTimeEntryAsync(postgres.ConnectionString, otherCompany, employee));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(EmployeeConstraint, error.ConstraintName);
    }

    [Fact]
    public async Task ATimeEntryOfAnEmployeeOfTheSameCompany_IsAccepted()
    {
        var company = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var employee = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, employee, NewEmail(), companyId: company);

        await CompanySeed.InsertTimeEntryAsync(postgres.ConnectionString, company, employee);

        Assert.Equal([company.ToString()], await MigrationDatabase.QueryAsync(postgres.ConnectionString, $"SELECT company_id::text FROM time_entries WHERE employee_id = '{employee}'"));
    }

    [Fact]
    public async Task ATimeEntryOfANonexistentEmployee_IsRejectedWithForeignKeyViolation()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => CompanySeed.InsertTimeEntryAsync(postgres.ConnectionString, Company.DefaultId, Guid.NewGuid()));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(EmployeeConstraint, error.ConstraintName);
    }

    [Fact]
    public async Task ARowOfANonexistentCompany_IsRejectedWithForeignKeyViolation()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => UserSeed.InsertRawAsync(postgres.ConnectionString, Guid.NewGuid(), NewEmail(), companyId: Guid.NewGuid()));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_users_companies_company_id", error.ConstraintName);
    }

    [Fact]
    public async Task DeletingAUserWithTimeEntries_IsRejectedAndKeepsBoth()
    {
        var employee = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, employee, NewEmail(), companyId: Company.DefaultId);
        await CompanySeed.InsertTimeEntryAsync(postgres.ConnectionString, Company.DefaultId, employee);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => MigrationDatabase.ExecuteAsync(postgres.ConnectionString, $"DELETE FROM users WHERE id = '{employee}'"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(EmployeeConstraint, error.ConstraintName);
        Assert.Equal(["1|1"], await MigrationDatabase.QueryAsync(postgres.ConnectionString, $"SELECT (SELECT count(*) FROM users WHERE id = '{employee}') || '|' || (SELECT count(*) FROM time_entries WHERE employee_id = '{employee}')"));
    }
}
