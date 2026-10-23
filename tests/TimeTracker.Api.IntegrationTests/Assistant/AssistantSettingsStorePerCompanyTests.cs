using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// El almacén generalizado (DD-13, TC-9): actúa solo sobre la fila de la compañía del contexto, ya no sobre la constante del puente.
/// La PK es <c>company_id</c>; el <c>xmin</c> de la fila de una compañía no cambia cuando otra guarda o borra.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantSettingsStorePerCompanyTests(PostgresFixture postgres)
{
    private static AuditEntry Audit(AuditAction action = AuditAction.Saved)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, action, CredentialChanged: false, Previous: null, Current: null);

    private static AssistantSettings Settings(string model, string version = "0")
        => new(AssistantProvider.Ollama, model, 25, 800, AssistantEffort.Low, Credential: null, Guid.NewGuid(), DateTimeOffset.UtcNow, version);

    private static string Token(SaveOutcome outcome) => Assert.IsType<SaveOutcome.Saved>(outcome).Token;

    private static Task<List<string>> Query(string connectionString, string sql) => MigrationDatabase.QueryAsync(connectionString, sql);

    private static Task<List<string>> XminOf(string connectionString, Guid companyId)
        => Query(connectionString, $"SELECT xmin::text FROM assistant_settings WHERE company_id = '{companyId}'");

    [Fact]
    public async Task EachCompanySavesAndReadsItsOwnRow_IncludingTheDefaultCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;

        Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-a"), "0", Audit(), CancellationToken.None));
        Token(await postgres.CreateAssistantStore(cs, fixture.B.Id).SaveAsync(Settings("m-b"), "0", Audit(), CancellationToken.None));
        Token(await postgres.CreateAssistantStore(cs, Company.DefaultId).SaveAsync(Settings("m-demo"), "0", Audit(), CancellationToken.None));

        Assert.Equal("m-a", (await postgres.CreateAssistantStore(cs, fixture.A.Id).GetAsync(CancellationToken.None))!.Model);
        Assert.Equal("m-b", (await postgres.CreateAssistantStore(cs, fixture.B.Id).GetAsync(CancellationToken.None))!.Model);
        Assert.Equal("m-demo", (await postgres.CreateAssistantStore(cs, Company.DefaultId).GetAsync(CancellationToken.None))!.Model);
        Assert.Equal(
            new[] { $"{Company.DefaultId}|m-demo", $"{fixture.A.Id}|m-a", $"{fixture.B.Id}|m-b" }.Order(),
            (await Query(cs, "SELECT company_id || '|' || model FROM assistant_settings")).Order());
    }

    [Theory]
    [InlineData(AssistantProvider.Gemini)]
    [InlineData(AssistantProvider.Groq)]
    public async Task EveryCatalogProvider_IsAcceptedByTheDatabaseConstraintAndReadBack(AssistantProvider provider)
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        var store = postgres.CreateAssistantStore(cs, fixture.A.Id);

        Token(await store.SaveAsync(Settings("m-a") with { Provider = provider }, "0", Audit(), CancellationToken.None));

        Assert.Equal(provider, (await store.GetAsync(CancellationToken.None))!.Provider);
        Assert.Equal([provider.ToString()], await Query(cs, $"SELECT provider FROM assistant_settings WHERE company_id = '{fixture.A.Id}'"));
    }

    [Fact]
    public async Task ACompanyWithoutARow_SeesNoRowAndTheNoRowToken_EvenWhenAnotherCompanyHasOne()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-a"), "0", Audit(), CancellationToken.None));
        var storeB = postgres.CreateAssistantStore(cs, fixture.B.Id);

        Assert.Null(await storeB.GetAsync(CancellationToken.None));
        Assert.Equal("0", await storeB.GetVersionAsync(CancellationToken.None));
        Assert.NotEqual("0", await postgres.CreateAssistantStore(cs, fixture.A.Id).GetVersionAsync(CancellationToken.None));
        Assert.Equal(DeleteOutcome.NotFound, await storeB.DeleteAsync("123", Audit(AuditAction.Reset), CancellationToken.None));
    }

    [Fact]
    public async Task TheFirstCreationOfBIsIndependentOfARowOfA()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-a"), "0", Audit(), CancellationToken.None));

        var saved = await postgres.CreateAssistantStore(cs, fixture.B.Id).SaveAsync(Settings("m-b"), "0", Audit(), CancellationToken.None);

        Assert.IsType<SaveOutcome.Saved>(saved);
    }

    [Fact]
    public async Task ASaveOrDeleteOfA_NeverChangesTheXminOrTheAuditOfB()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        var tokenA = Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-a"), "0", Audit(), CancellationToken.None));
        Token(await postgres.CreateAssistantStore(cs, fixture.B.Id).SaveAsync(Settings("m-b"), "0", Audit(), CancellationToken.None));
        var xminOfB = await XminOf(cs, fixture.B.Id);
        var auditOfB = await Query(cs, $"SELECT count(*)::text FROM assistant_settings_audit WHERE company_id = '{fixture.B.Id}'");

        var tokenA2 = Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-a2"), tokenA, Audit(), CancellationToken.None));
        Assert.Equal(xminOfB, await XminOf(cs, fixture.B.Id));

        Assert.Equal(DeleteOutcome.Deleted, await postgres.CreateAssistantStore(cs, fixture.A.Id).DeleteAsync(tokenA2, Audit(AuditAction.Reset), CancellationToken.None));
        Assert.Equal(xminOfB, await XminOf(cs, fixture.B.Id));
        Assert.Equal(auditOfB, await Query(cs, $"SELECT count(*)::text FROM assistant_settings_audit WHERE company_id = '{fixture.B.Id}'"));
        Assert.Equal("m-b", (await postgres.CreateAssistantStore(cs, fixture.B.Id).GetAsync(CancellationToken.None))!.Model);
    }

    [Fact]
    public async Task TheAuditIsInsertedWithTheCompanyOfTheContext_InTheSameTransaction()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;

        Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-a"), "0", Audit(), CancellationToken.None));
        Token(await postgres.CreateAssistantStore(cs, fixture.B.Id).SaveAsync(Settings("m-b"), "0", Audit(), CancellationToken.None));

        Assert.Equal(
            new[] { fixture.A.Id.ToString(), fixture.B.Id.ToString() }.Order(),
            (await Query(cs, "SELECT company_id::text FROM assistant_settings_audit")).Order());
    }

    [Fact]
    public async Task AConcurrentInsertOfTheSameCompany_ViolatesThePrimaryKeyAndIsAConflict()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("primero"), "0", Audit(), CancellationToken.None));

        var second = await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("segundo"), "0", Audit(), CancellationToken.None);

        Assert.IsType<SaveOutcome.Conflict>(second);
        Assert.Equal(["primero"], await Query(cs, $"SELECT model FROM assistant_settings WHERE company_id = '{fixture.A.Id}'"));
        Assert.Equal(["1"], await Query(cs, $"SELECT count(*)::text FROM assistant_settings_audit WHERE company_id = '{fixture.A.Id}'"));
    }

    [Fact]
    public async Task AStaleXmin_IsAConflictAndTheXminIsPreservedAcrossSaves()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        var first = Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-1"), "0", Audit(), CancellationToken.None));
        var second = Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-2"), first, Audit(), CancellationToken.None));

        var stale = await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-3"), first, Audit(), CancellationToken.None);

        Assert.NotEqual(first, second);
        Assert.IsType<SaveOutcome.Conflict>(stale);
        Assert.Equal([second], await XminOf(cs, fixture.A.Id));
        Assert.Equal(DeleteOutcome.Conflict, await postgres.CreateAssistantStore(cs, fixture.A.Id).DeleteAsync(first, Audit(AuditAction.Reset), CancellationToken.None));
    }

    [Fact]
    public async Task ATokenReadByAnotherCompany_IsAConflictAndChangesNoRow()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-a"), "0", Audit(), CancellationToken.None));
        var tokenOfB = Token(await postgres.CreateAssistantStore(cs, fixture.B.Id).SaveAsync(Settings("m-b"), "0", Audit(), CancellationToken.None));
        var xminOfA = await XminOf(cs, fixture.A.Id);
        var xminOfB = await XminOf(cs, fixture.B.Id);

        var outcome = await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-x"), tokenOfB, Audit(), CancellationToken.None);

        Assert.IsType<SaveOutcome.Conflict>(outcome);
        Assert.Equal(xminOfA, await XminOf(cs, fixture.A.Id));
        Assert.Equal(xminOfB, await XminOf(cs, fixture.B.Id));
        Assert.Equal(["2"], await Query(cs, "SELECT count(*)::text FROM assistant_settings_audit"));
    }

    [Fact]
    public async Task WithoutACompanyInTheContext_ReadsAreEmptyAndWritesAreRefused()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var cs = fixture.ConnectionString;
        Token(await postgres.CreateAssistantStore(cs, fixture.A.Id).SaveAsync(Settings("m-a"), "0", Audit(), CancellationToken.None));
        var withoutCompany = postgres.CreateAssistantStoreWithoutCompany(cs);

        Assert.Null(await withoutCompany.GetAsync(CancellationToken.None));
        Assert.Equal("0", await withoutCompany.GetVersionAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => withoutCompany.SaveAsync(Settings("m-x"), "0", Audit(), CancellationToken.None));
        Assert.Equal(["1"], await Query(cs, "SELECT count(*)::text FROM assistant_settings"));
    }
}
