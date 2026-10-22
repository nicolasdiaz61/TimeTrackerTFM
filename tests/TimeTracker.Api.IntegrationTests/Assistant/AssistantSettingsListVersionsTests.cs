using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// TC-11 (DD-15): el sondeo global lee en una sola consulta, sin entidades con seguimiento, la versión (<c>xmin</c>) de la
/// configuración de todas las compañías y su indicador <c>uses_platform_assistant</c>. Es el sitio 2 de la lista cerrada de <c>IgnoreQueryFilters</c>.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantSettingsListVersionsTests(PostgresFixture postgres)
{
    private static AuditEntry Audit(AuditAction action = AuditAction.Saved)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, action, CredentialChanged: false, Previous: null, Current: null);

    private static AssistantSettings Settings(string model)
        => new(AssistantProvider.Ollama, model, 25, 800, AssistantEffort.Low, Credential: null, Guid.NewGuid(), DateTimeOffset.UtcNow, "0");

    private static async Task<string> SaveAsync(PostgresFixture postgres, string connectionString, Guid company, string model, string token = "0")
        => Assert.IsType<SaveOutcome.Saved>(await postgres.CreateAssistantStore(connectionString, company).SaveAsync(Settings(model), token, Audit(), CancellationToken.None)).Token;

    private static (AssistantSettingsStore Store, CommandCounter Counter, TimeTrackerDbContext Context) PollerStore(string connectionString)
    {
        var counter = new CommandCounter();
        var companyless = new TestCompanyContext(null);
        var context = new TimeTrackerDbContext(
            new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(connectionString).AddInterceptors(counter).Options, companyless);
        return (new AssistantSettingsStore(context, companyless), counter, context);
    }

    [Fact]
    public async Task ListVersions_ReadsEveryCompanyInOneQueryWithoutTrackedEntities_FromAContextWithoutCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        var tokenA = await SaveAsync(postgres, cs, fixture.A.Id, "m-a");
        var tokenDemo = await SaveAsync(postgres, cs, Company.DefaultId, "m-demo");
        var (store, counter, context) = PollerStore(cs);
        await using var _ = context;

        var versions = await store.ListVersionsAsync(CancellationToken.None);

        Assert.Equal(1, counter.Reads);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(3, versions.Count);
        Assert.Equal(new CompanyVersion(fixture.A.Id, tokenA, false), versions.Single(entry => entry.CompanyId == fixture.A.Id));
        Assert.Equal(new CompanyVersion(Company.DefaultId, tokenDemo, true), versions.Single(entry => entry.CompanyId == Company.DefaultId));
        Assert.Equal(new CompanyVersion(fixture.B.Id, "0", false), versions.Single(entry => entry.CompanyId == fixture.B.Id));
    }

    [Fact]
    public async Task ListVersions_FollowsEverySaveAndDelete_AndTheVersionIsTheOneTheStoreHandsOut()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        var first = await SaveAsync(postgres, cs, fixture.B.Id, "m-1");
        var second = await SaveAsync(postgres, cs, fixture.B.Id, "m-2", first);
        var (store, _, context) = PollerStore(cs);
        await using var _ = context;

        var afterSaves = (await store.ListVersionsAsync(CancellationToken.None)).Single(entry => entry.CompanyId == fixture.B.Id);
        Assert.Equal(second, afterSaves.Version);
        Assert.NotEqual(first, afterSaves.Version);
        Assert.Equal(second, await postgres.CreateAssistantStore(cs, fixture.B.Id).GetVersionAsync(CancellationToken.None));

        Assert.Equal(DeleteOutcome.Deleted, await postgres.CreateAssistantStore(cs, fixture.B.Id).DeleteAsync(second, Audit(AuditAction.Reset), CancellationToken.None));
        context.ChangeTracker.Clear();
        var afterDelete = (await store.ListVersionsAsync(CancellationToken.None)).Single(entry => entry.CompanyId == fixture.B.Id);
        Assert.Equal("0", afterDelete.Version);
    }

    [Fact]
    public async Task ListVersions_ReportsAChangeOfThePlatformIndicator()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        var (store, _, context) = PollerStore(cs);
        await using var _ = context;
        Assert.True((await store.ListVersionsAsync(CancellationToken.None)).Single(entry => entry.CompanyId == Company.DefaultId).UsesPlatformAssistant);

        await using (var connection = new Npgsql.NpgsqlConnection(cs))
        {
            await connection.OpenAsync(CancellationToken.None);
            await using var command = new Npgsql.NpgsqlCommand("UPDATE companies SET uses_platform_assistant = false WHERE id = @id", connection);
            command.Parameters.AddWithValue("id", Company.DefaultId);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        Assert.False((await store.ListVersionsAsync(CancellationToken.None)).Single(entry => entry.CompanyId == Company.DefaultId).UsesPlatformAssistant);
    }

    [Fact]
    public async Task ListVersions_WithoutTheTables_ReturnsNoCompanies()
    {
        var cs = await postgres.CreateEmptyDatabaseAsync();
        var (store, _, context) = PollerStore(cs);
        await using var _ = context;

        Assert.Empty(await store.ListVersionsAsync(CancellationToken.None));
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _reads);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
