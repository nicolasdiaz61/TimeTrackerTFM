using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// TC-4 (DD-8): el filtro global falla cerrado. Un contexto sin compañía, o con una compañía vacía, no ve ninguna fila y no
/// lanza; con una compañía solo ve las suyas, también en uniones y agregados; <c>companies</c> no lleva filtro.
/// </summary>
[Collection("Postgres")]
public class FailClosedFilterTests(PostgresFixture postgres)
{
    // Cada compañía tiene 3 personas (un Manager y dos Employees), 3 fichajes, una fila de configuración y una de auditoría.
    private const int UsersPerCompany = 3;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutACompanyOrWithAnEmptyOne_EveryDbSetWithCompanyIdReturnsZeroRows_AndDoesNotThrow(bool emptyGuid)
    {
        var (connectionString, _, _) = await SeededAsync();
        await using var context = ContextFor(connectionString, emptyGuid ? Guid.Empty : null);

        Assert.Equal(0, await context.Users.CountAsync());
        Assert.Equal(0, await context.TimeEntries.CountAsync());
        Assert.Equal(0, await context.Set<AssistantSettingsRecord>().CountAsync());
        Assert.Equal(0, await context.Set<AssistantSettingsAuditRecord>().CountAsync());

        // Control: las filas existen en la base (sin ellas el cero no probaría nada).
        Assert.Equal(["6", "6", "2", "2"], await MigrationDatabase.QueryAsync(
            connectionString,
            "SELECT count(*)::text FROM users WHERE company_id IN (SELECT id FROM companies WHERE id <> '" + Company.DefaultId + "') UNION ALL SELECT count(*)::text FROM time_entries UNION ALL SELECT count(*)::text FROM assistant_settings UNION ALL SELECT count(*)::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task WithACompany_EveryDbSetReturnsOnlyThatCompanysRows()
    {
        var (connectionString, a, b) = await SeededAsync();

        foreach (var company in new[] { a, b })
        {
            await using var context = postgres.CreateContext(connectionString, company.Id);

            Assert.Equal(UsersPerCompany, await context.Users.CountAsync());
            Assert.All(await context.Users.AsNoTracking().ToListAsync(), user => Assert.Equal(company.Id, user.CompanyId));
            Assert.Equal(UsersPerCompany, await context.TimeEntries.CountAsync());
            Assert.All(await context.TimeEntries.AsNoTracking().ToListAsync(), entry => Assert.Equal(company.Id, entry.CompanyId));
            Assert.Equal([company.Id], await context.Set<AssistantSettingsRecord>().Select(row => row.CompanyId).ToListAsync());
            Assert.Equal([company.Id], await context.Set<AssistantSettingsAuditRecord>().Select(row => row.CompanyId).ToListAsync());
        }
    }

    [Fact]
    public async Task JoinsUnionsAndAggregates_NeverMixInRowsOfAnotherCompany()
    {
        var (connectionString, a, b) = await SeededAsync();
        await using var context = postgres.CreateContext(connectionString, a.Id);

        var joined = await context.Users
            .Join(context.TimeEntries, user => user.Id, entry => entry.EmployeeId, (user, entry) => new { user.CompanyId, EntryCompany = entry.CompanyId })
            .ToListAsync();
        var united = await context.Users.Select(user => user.Id).Concat(context.TimeEntries.Select(entry => entry.EmployeeId)).ToListAsync();
        var perCompany = await context.TimeEntries.GroupBy(entry => entry.CompanyId).Select(group => new { group.Key, Count = group.Count() }).ToListAsync();

        Assert.Equal(UsersPerCompany, joined.Count);
        Assert.All(joined, row => Assert.True(row.CompanyId == a.Id && row.EntryCompany == a.Id));
        Assert.Equal(2 * UsersPerCompany, united.Count);
        Assert.Empty(united.Intersect(b.All.Select(member => member.Id)));
        Assert.Equal([(a.Id, UsersPerCompany)], perCompany.Select(row => (row.Key, row.Count)));
    }

    [Fact]
    public async Task Companies_HasNoFilter_AndIsReadableWithAnyContext()
    {
        var (connectionString, a, b) = await SeededAsync();

        foreach (var companyContext in new Guid?[] { null, Guid.Empty, a.Id })
        {
            await using var context = ContextFor(connectionString, companyContext);
            var ids = await context.Companies.Select(company => company.Id).ToListAsync();

            Assert.Contains(a.Id, ids);
            Assert.Contains(b.Id, ids);
        }
    }

    [Fact]
    public async Task TheAssistantBridge_StillSeesItsRowWithTheFilterActive()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var migrating = postgres.CreateContext(connectionString))
        {
            await migrating.Database.MigrateAsync(CancellationToken.None);
        }

        await MigrationDatabase.ExecuteAsync(connectionString, $"INSERT INTO assistant_settings (company_id, provider, model, updated_by, updated_at) VALUES ('{Company.DefaultId}', 'Ollama', 'modelo-puente', gen_random_uuid(), now())");

        var withDefaultCompany = await new AssistantSettingsStore(postgres.CreateContext(connectionString, Company.DefaultId)).GetAsync(CancellationToken.None);
        var withoutCompany = await new AssistantSettingsStore(ContextFor(connectionString, null)).GetAsync(CancellationToken.None);

        Assert.Equal("modelo-puente", withDefaultCompany?.Model);
        Assert.Null(withoutCompany);
    }

    private static TimeTrackerDbContext ContextFor(string connectionString, Guid? companyId)
        => new(new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(connectionString).Options, new TestCompanyContext(companyId));

    private async Task<(string ConnectionString, TwoCompanyFixture.CompanyMembers A, TwoCompanyFixture.CompanyMembers B)> SeededAsync()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        foreach (var company in new[] { fixture.A, fixture.B })
        {
            await MigrationDatabase.ExecuteAsync(fixture.ConnectionString, $"INSERT INTO assistant_settings (company_id, provider, model, updated_by, updated_at) VALUES ('{company.Id}', 'Ollama', 'modelo-{company.Name}', gen_random_uuid(), now())");
            await MigrationDatabase.ExecuteAsync(fixture.ConnectionString, $"INSERT INTO assistant_settings_audit (company_id, occurred_at, actor_id, action, credential_changed) VALUES ('{company.Id}', now(), gen_random_uuid(), 'Saved', false)");
        }

        return (fixture.ConnectionString, fixture.A, fixture.B);
    }
}
