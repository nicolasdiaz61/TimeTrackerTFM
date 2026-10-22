using Microsoft.Extensions.DependencyInjection;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Companies;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Persistence;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>El lector del registro lee siempre dentro de <c>CreateScope(companyId)</c>: la fila y el indicador son los de esa compañía.</summary>
public sealed class CompanySettingsReaderTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.Parse("00000000-0000-4000-8000-0000000000a1");
    private static readonly Guid CompanyB = Guid.Parse("00000000-0000-4000-8000-0000000000b1");

    private readonly FakeAssistantSettingsStore _store = new();
    private readonly InMemoryCompanyRepository _companies = new();
    private readonly List<Guid?> _storeCompanies = [];
    private readonly ServiceProvider _provider;
    private readonly RecordingScopeFactory _scopes;
    private readonly CompanySettingsReader _reader;

    public CompanySettingsReaderTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICompanyRepository>(_ => _companies);
        services.AddScoped<RecordingCompanyContext>();
        services.AddScoped<ICompanyContext>(scope => scope.GetRequiredService<RecordingCompanyContext>());
        services.AddScoped<ICompanyContextSetter>(scope => scope.GetRequiredService<RecordingCompanyContext>());
        services.AddScoped<IAssistantSettingsStore>(scope =>
        {
            _storeCompanies.Add(scope.GetRequiredService<ICompanyContext>().CompanyId);
            return _store;
        });
        _provider = services.BuildServiceProvider();
        _scopes = new RecordingScopeFactory(new CompanyScopeFactory(_provider.GetRequiredService<IServiceScopeFactory>()));
        _reader = new CompanySettingsReader(_scopes);
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task ReadAsync_OpensTheScopeOfTheGivenCompany_ForTheStoreAndTheCompanyRepository()
    {
        _companies.Seed(CompanyA, "A", usesPlatformAssistant: false);
        _companies.Seed(CompanyB, "B", usesPlatformAssistant: true);

        var a = await _reader.ReadAsync(CompanyA, CancellationToken.None);
        var b = await _reader.ReadAsync(CompanyB, CancellationToken.None);

        Assert.Equal([CompanyA, CompanyB], _scopes.Requested);
        Assert.Equal([(Guid?)CompanyA, CompanyB], _storeCompanies);
        Assert.Equal([CompanyA, CompanyB], _companies.LoginInfoRequests);
        Assert.False(a.UsesPlatform);
        Assert.True(b.UsesPlatform);
    }

    [Fact]
    public async Task ReadAsync_ReturnsTheStoredRow_OrNullWithoutOne()
    {
        _companies.Seed(CompanyA, "A");
        _store.Stored = new AssistantSettings(AssistantProvider.Ollama, "db-m", null, null, null, null, Guid.NewGuid(), DateTimeOffset.UnixEpoch, "5");

        Assert.Equal("5", (await _reader.ReadAsync(CompanyA, CancellationToken.None)).Stored!.Version);
        _store.Stored = null;
        Assert.Null((await _reader.ReadAsync(CompanyA, CancellationToken.None)).Stored);
    }

    [Fact]
    public async Task ReadAsync_AnUnknownCompany_IsNotPlatform()
    {
        Assert.False((await _reader.ReadAsync(CompanyA, CancellationToken.None)).UsesPlatform);
    }

    [Theory]
    [InlineData("00000000-0000-7000-8000-000000000001", true)]
    [InlineData("00000000-0000-4000-8000-0000000000a1", false)]
    public async Task ReadAsync_WhenTheCompaniesTableDoesNotExistYet_OnlyTheDefaultCompanyUsesTheEnvironment(string company, bool expected)
    {
        _companies.Failure = new Npgsql.PostgresException("no existe la tabla", "ERROR", "ERROR", Npgsql.PostgresErrorCodes.UndefinedTable);

        Assert.Equal(expected, (await _reader.ReadAsync(Guid.Parse(company), CancellationToken.None)).UsesPlatform);
    }

    [Fact]
    public async Task ReadAsync_WhenTheDatabaseFailsForAnotherReason_Propagates()
    {
        _companies.Failure = new TimeoutException("base de datos caída");

        await Assert.ThrowsAsync<TimeoutException>(() => _reader.ReadAsync(CompanyA, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_WhenTheStoreFails_Propagates()
    {
        _store.Failure = new TimeoutException("base de datos caída");

        await Assert.ThrowsAsync<TimeoutException>(() => _reader.ReadAsync(CompanyA, CancellationToken.None));
    }

    [Fact]
    public void TheDefaultCompanyConstant_IsTheOneUsedForThePlatformFallback() => Assert.Equal(Guid.Parse("00000000-0000-7000-8000-000000000001"), Company.DefaultId);
}
