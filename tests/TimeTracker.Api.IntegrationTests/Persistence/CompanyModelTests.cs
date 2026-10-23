using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// El modelo de EF refleja el diseño de compañías (DD-2) y, creado desde cero con PostgreSQL real, genera las claves
/// compuestas esperadas (V-5). La migración A deja las columnas <c>company_id</c> anulables; esa diferencia con el
/// modelo final la cubren las pruebas de migración.
/// </summary>
[Collection("Postgres")]
public class CompanyModelTests(PostgresFixture postgres)
{
    private static readonly Guid CompanyA = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0a01");
    private static readonly Guid CompanyB = Guid.Parse("0197a1b2-bbbb-7e4f-8a5b-6c7d8e9f0a02");

    [Fact]
    public void Model_MapsCompaniesWithItsSixColumnsAndConstraints()
    {
        using var context = postgres.CreateContext(postgres.ConnectionString);
        var company = DesignModel(context).FindEntityType(typeof(Company))!;

        Assert.Equal("companies", company.GetTableName());
        Assert.Equal(
            ["created_at", "id", "name", "name_normalized", "status", "uses_platform_assistant"],
            company.GetProperties().Select(property => property.GetColumnName()).Order(StringComparer.Ordinal));
        Assert.All(company.GetProperties(), property => Assert.False(property.IsNullable));
        Assert.Equal(["id"], company.FindPrimaryKey()!.Properties.Select(property => property.GetColumnName()));
        Assert.Equal(false, company.FindProperty(nameof(Company.UsesPlatformAssistant))!.GetDefaultValue());
        Assert.Equal(100, company.FindProperty(nameof(Company.Name))!.GetMaxLength());
        var unique = Assert.Single(company.GetIndexes());
        Assert.Equal("ux_companies_name_normalized", unique.GetDatabaseName());
        Assert.True(unique.IsUnique);
        Assert.Equal(["name_normalized"], unique.Properties.Select(property => property.GetColumnName()));
        Assert.Equal(
            ["ck_companies_name_length", "ck_companies_status"],
            company.GetCheckConstraints().Select(check => check.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("TimeTracker.Domain.Users.UserAccount")]
    [InlineData("TimeTracker.Domain.TimeTracking.TimeEntry")]
    [InlineData("TimeTracker.Infrastructure.Persistence.AssistantSettingsRecord")]
    [InlineData("TimeTracker.Infrastructure.Persistence.AssistantSettingsAuditRecord")]
    public void Model_AddsARequiredCompanyIdToEveryBusinessTable(string entity)
    {
        using var context = postgres.CreateContext(postgres.ConnectionString);
        var property = DesignModel(context).FindEntityType(entity)!.FindProperty("CompanyId")!;

        Assert.Equal("company_id", property.GetColumnName());
        Assert.Equal(typeof(Guid), property.ClrType);
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void Model_MapsTheUsersCompanyKeysAndTheTemporaryPasswordMark()
    {
        using var context = postgres.CreateContext(postgres.ConnectionString);
        var users = DesignModel(context).FindEntityType(typeof(UserAccount))!;

        var mark = users.FindProperty(nameof(UserAccount.MustChangePassword))!;
        Assert.Equal("must_change_password", mark.GetColumnName());
        Assert.Equal(false, mark.GetDefaultValue());

        var alternate = Assert.Single(users.GetKeys(), key => !key.IsPrimaryKey());
        Assert.Equal(["company_id", "id"], alternate.Properties.Select(property => property.GetColumnName()));
        Assert.Equal("ux_users_company_id_id", alternate.GetName());

        var toCompany = Assert.Single(users.GetForeignKeys(), key => key.Properties.Count == 1);
        Assert.Equal("fk_users_companies_company_id", toCompany.GetConstraintName());
        Assert.Equal(typeof(Company), toCompany.PrincipalEntityType.ClrType);
        var manager = Assert.Single(users.GetForeignKeys(), key => key.Properties.Count == 2 && key.PrincipalEntityType.ClrType == typeof(TimeTracker.Domain.Users.UserAccount));
        Assert.Equal("fk_users_users_company_id_manager_id", manager.GetConstraintName());
        Assert.Equal(["company_id", "manager_id"], manager.Properties.Select(property => property.GetColumnName()));
        Assert.Equal(["company_id", "id"], manager.PrincipalKey.Properties.Select(property => property.GetColumnName()));
        Assert.Equal(DeleteBehavior.NoAction, manager.DeleteBehavior);
        Assert.False(manager.IsRequired);

        Assert.Contains(users.GetIndexes(), index => index.GetDatabaseName() == "ix_users_company_id_manager_id"
            && index.Properties.Select(property => property.GetColumnName()).SequenceEqual(["company_id", "manager_id"]));
        Assert.Contains(users.GetIndexes(), index => index.GetDatabaseName() == "ux_users_email" && index.IsUnique);
    }

    [Fact]
    public void Model_UsesXminAsTheConcurrencyTokenOfUsersWithoutANewColumn()
    {
        using var context = postgres.CreateContext(postgres.ConnectionString);
        var xmin = DesignModel(context).FindEntityType(typeof(UserAccount))!.FindProperty("xmin")!;

        Assert.True(xmin.IsConcurrencyToken);
        Assert.Equal("xid", xmin.GetColumnType());
    }

    [Fact]
    public void Model_MapsTheCompositeForeignKeyOfTimeEntriesToTheirEmployee()
    {
        using var context = postgres.CreateContext(postgres.ConnectionString);
        var entries = DesignModel(context).FindEntityType(typeof(TimeEntry))!;

        var toCompany = Assert.Single(entries.GetForeignKeys(), key => key.Properties.Count == 1);
        Assert.Equal("fk_time_entries_companies_company_id", toCompany.GetConstraintName());
        var employee = Assert.Single(entries.GetForeignKeys(), key => key.Properties.Count == 2);
        Assert.Equal("fk_time_entries_users_company_id_employee_id", employee.GetConstraintName());
        Assert.Equal(["company_id", "employee_id"], employee.Properties.Select(property => property.GetColumnName()));
        Assert.Equal(["company_id", "id"], employee.PrincipalKey.Properties.Select(property => property.GetColumnName()));
        Assert.Equal(DeleteBehavior.NoAction, employee.DeleteBehavior);
        Assert.Equal(typeof(UserAccount), employee.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void Model_IndexesTheAuditByCompanyAndOccurrence()
    {
        using var context = postgres.CreateContext(postgres.ConnectionString);
        var audit = DesignModel(context).FindEntityType("TimeTracker.Infrastructure.Persistence.AssistantSettingsAuditRecord")!;

        var index = Assert.Single(audit.GetIndexes());
        Assert.Equal("ix_assistant_settings_audit_company_id_occurred_at", index.GetDatabaseName());
        Assert.Equal(["company_id", "occurred_at"], index.Properties.Select(property => property.GetColumnName()));
        Assert.Equal([false, true], index.IsDescending);
    }

    [Fact]
    public async Task FinalModelSchema_ChecksCompanyScopeOfManagersAndTimeEntries_V5()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await ExecuteAsync(connectionString, context.Database.GenerateCreateScript());
        }

        await ExecuteAsync(connectionString, $"INSERT INTO companies (id, name, name_normalized, status, uses_platform_assistant, created_at) VALUES ('{CompanyA}', 'A', 'a', 'Active', false, now()), ('{CompanyB}', 'B', 'b', 'Active', false, now())");
        var managerB = Guid.NewGuid();
        var managerA = Guid.NewGuid();
        await InsertUserAsync(connectionString, managerA, CompanyA, "ma@example.test", managerId: null);
        await InsertUserAsync(connectionString, managerB, CompanyB, "mb@example.test", managerId: null);

        // Con manager_id nulo y company_id presente, MATCH SIMPLE no valida nada.
        await InsertUserAsync(connectionString, Guid.NewGuid(), CompanyA, "sin.responsable@example.test", managerId: null);
        await InsertUserAsync(connectionString, Guid.NewGuid(), CompanyA, "con.responsable@example.test", managerA);
        var crossCompany = await Assert.ThrowsAsync<PostgresException>(() => InsertUserAsync(connectionString, Guid.NewGuid(), CompanyA, "ajeno@example.test", managerB));
        var foreignEntry = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connectionString, $"INSERT INTO time_entries (id, company_id, employee_id, clock_in_at, type) VALUES (gen_random_uuid(), '{CompanyB}', '{managerA}', now(), 'Work')"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, crossCompany.SqlState);
        Assert.Equal("fk_users_users_company_id_manager_id", crossCompany.ConstraintName);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreignEntry.SqlState);
        Assert.Equal("fk_time_entries_users_company_id_employee_id", foreignEntry.ConstraintName);
        var details = await QueryAsync(
            connectionString,
            "SELECT confdeltype::text || confupdtype::text || confmatchtype::text || condeferrable::text FROM pg_constraint WHERE conname = 'fk_users_users_company_id_manager_id'");
        Assert.Equal(["aasfalse"], details);
    }

    [Fact]
    public async Task Xmin_RejectsTheSecondOfTwoConcurrentSavesOfTheSameUser()
    {
        var user = UserSeed.Create("concurrente");
        await using (var setup = postgres.CreateContext(postgres.ConnectionString))
        {
            await UserSeed.AddAsync(setup, user);
        }

        await using var first = postgres.CreateContext(postgres.ConnectionString);
        await using var second = postgres.CreateContext(postgres.ConnectionString);
        var fromFirst = await first.Users.SingleAsync(candidate => candidate.Id == user.Id);
        var fromSecond = await second.Users.SingleAsync(candidate => candidate.Id == user.Id);

        fromFirst.ChangePassword("hash-primero");
        await first.SaveChangesAsync();
        fromSecond.ChangePassword("hash-segundo");

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = postgres.CreateContext(postgres.ConnectionString);
        Assert.Equal("hash-primero", (await check.Users.SingleAsync(candidate => candidate.Id == user.Id)).PasswordHash);
    }

    private static IModel DesignModel(DbContext context) => context.GetService<IDesignTimeModel>().Model;

    private static Task InsertUserAsync(string connectionString, Guid id, Guid companyId, string email, Guid? managerId)
        => ExecuteAsync(
            connectionString,
            $"INSERT INTO users (id, company_id, email, display_name, password_hash, role, manager_id, must_change_password) VALUES ('{id}', '{companyId}', '{email}', 'Usuario', 'hash', 'Employee', {(managerId is null ? "NULL" : $"'{managerId}'")}, false)");

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task<List<string>> QueryAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var rows = new List<string>();
        while (await reader.ReadAsync(CancellationToken.None))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
