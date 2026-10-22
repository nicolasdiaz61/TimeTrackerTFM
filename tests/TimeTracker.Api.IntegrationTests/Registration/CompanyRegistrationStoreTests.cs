using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.Security;
using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>
/// El almacén del autorregistro abre un ámbito de la compañía nueva y escribe compañía y Manager con un único guardado (DD-19, DD-7).
/// La unicidad la deciden las restricciones de la base de datos, sin consulta previa.
/// </summary>
[Collection("Postgres")]
public class CompanyRegistrationStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_WritesTheCompanyAndItsManagerInOneSaveInsideTheScopeOfTheNewCompany()
    {
        var connectionString = await MigratedDatabaseAsync();
        using var host = new StoreHost(connectionString);
        var (company, manager) = NewRegistration("Acme Norte", "ana@example.test");

        var outcome = await host.Store.CreateAsync(company, manager, CancellationToken.None);

        Assert.Equal(RegistrationOutcome.Created, outcome);
        Assert.Equal([company.Id], host.RequestedScopes);
        Assert.Equal(1, host.Saves);
        Assert.Equal(
            [$"{company.Id}|Acme Norte|acme norte|Active|false"],
            await QueryAsync(connectionString, $"SELECT id || '|' || name || '|' || name_normalized || '|' || status || '|' || uses_platform_assistant FROM companies WHERE id = '{company.Id}'"));
        Assert.Equal(
            [$"{manager.Id}|{company.Id}|ana@example.test|Manager|false|true"],
            await QueryAsync(connectionString, $"SELECT id || '|' || company_id || '|' || email || '|' || role || '|' || must_change_password || '|' || (manager_id IS NULL) FROM users WHERE company_id = '{company.Id}'"));
    }

    [Fact]
    public async Task CreateAsync_WithADuplicateNormalizedName_ReturnsDuplicateNameAndWritesNothing()
    {
        var connectionString = await MigratedDatabaseAsync();
        using var host = new StoreHost(connectionString);
        var first = NewRegistration("Acme Norte", "ana@example.test");
        await host.Store.CreateAsync(first.Company, first.Manager, CancellationToken.None);
        var second = NewRegistration("  ACME   norte  ", "luis@example.test");

        var outcome = await host.Store.CreateAsync(second.Company, second.Manager, CancellationToken.None);

        Assert.Equal(RegistrationOutcome.DuplicateName, outcome);
        Assert.Equal(["1"], await QueryAsync(connectionString, "SELECT count(*)::text FROM companies WHERE name_normalized = 'acme norte'"));
        Assert.Equal(["0"], await QueryAsync(connectionString, "SELECT count(*)::text FROM users WHERE email = 'luis@example.test'"));
    }

    [Fact]
    public async Task CreateAsync_WithAnEmailAlreadyRegisteredInAnotherCompany_ReturnsDuplicateEmailAndLeavesNoCompanyBehind()
    {
        var connectionString = await MigratedDatabaseAsync();
        using var host = new StoreHost(connectionString);
        var first = NewRegistration("Acme Norte", "ana@example.test");
        await host.Store.CreateAsync(first.Company, first.Manager, CancellationToken.None);
        var second = NewRegistration("Otra Compañía", "ana@example.test");

        var outcome = await host.Store.CreateAsync(second.Company, second.Manager, CancellationToken.None);

        // Atomicidad: el fallo al insertar el usuario deshace también la compañía, que se insertó en el mismo guardado.
        Assert.Equal(RegistrationOutcome.DuplicateEmail, outcome);
        Assert.Equal(["0"], await QueryAsync(connectionString, "SELECT count(*)::text FROM companies WHERE name_normalized = 'otra compañía'"));
        Assert.Equal(["1"], await QueryAsync(connectionString, "SELECT count(*)::text FROM users WHERE email = 'ana@example.test'"));
    }

    [Fact]
    public async Task CreateAsync_WithAnAccentedNameThatDiffersOnlyByTheAccent_CreatesAnotherCompany()
    {
        var connectionString = await MigratedDatabaseAsync();
        using var host = new StoreHost(connectionString);
        var plain = NewRegistration("Acme Norte", "ana@example.test");
        var accented = NewRegistration("Ácme Norte", "luis@example.test");

        Assert.Equal(RegistrationOutcome.Created, await host.Store.CreateAsync(plain.Company, plain.Manager, CancellationToken.None));
        Assert.Equal(RegistrationOutcome.Created, await host.Store.CreateAsync(accented.Company, accented.Manager, CancellationToken.None));

        Assert.Equal(["acme norte", "ácme norte"], await QueryAsync(connectionString, "SELECT name_normalized FROM companies WHERE name_normalized IN ('acme norte', 'ácme norte') ORDER BY 1"));
    }

    [Fact]
    public async Task CreateAsync_WithTwoSimultaneousIdenticalRegistrations_CreatesExactlyOneCompany()
    {
        var connectionString = await MigratedDatabaseAsync();
        using var host = new StoreHost(connectionString);
        var attempts = Enumerable.Range(0, 2).Select(_ => NewRegistration("Acme Norte", "ana@example.test")).ToList();

        var outcomes = await Task.WhenAll(attempts.Select(attempt => host.Store.CreateAsync(attempt.Company, attempt.Manager, CancellationToken.None)));

        Assert.Single(outcomes, outcome => outcome == RegistrationOutcome.Created);
        Assert.Single(outcomes, outcome => outcome != RegistrationOutcome.Created);
        Assert.Equal(["1"], await QueryAsync(connectionString, "SELECT count(*)::text FROM companies WHERE name_normalized = 'acme norte'"));
        Assert.Equal(["1"], await QueryAsync(connectionString, "SELECT count(*)::text FROM users WHERE email = 'ana@example.test'"));
    }

    [Fact]
    public async Task CreateAsync_WithAnUnrelatedUniqueViolation_IsNotMappedToADuplicateOutcome()
    {
        var connectionString = await MigratedDatabaseAsync();
        using var host = new StoreHost(connectionString);
        var first = NewRegistration("Acme Norte", "ana@example.test");
        await host.Store.CreateAsync(first.Company, first.Manager, CancellationToken.None);
        // Mismo identificador de compañía: choca con la clave primaria, que no es ni el nombre ni el correo.
        var clash = Company.Create(first.Company.Id, "Otro Nombre", Now);
        var manager = UserAccount.Create(Guid.NewGuid(), clash.Id, "otro@example.test", "Otro", "hash", UserRole.Manager, null);

        await Assert.ThrowsAsync<DbUpdateException>(() => host.Store.CreateAsync(clash, manager, CancellationToken.None));

        Assert.Equal(["0"], await QueryAsync(connectionString, "SELECT count(*)::text FROM users WHERE email = 'otro@example.test'"));
    }

    [Fact]
    public async Task CreateAsync_WithAManagerOfAnotherCompany_ThrowsAndWritesNothing()
    {
        var connectionString = await MigratedDatabaseAsync();
        using var host = new StoreHost(connectionString);
        var company = Company.Create(Guid.NewGuid(), "Acme Norte", Now);
        var manager = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "ana@example.test", "Ana", "hash", UserRole.Manager, null);

        await Assert.ThrowsAsync<ArgumentException>(() => host.Store.CreateAsync(company, manager, CancellationToken.None));

        Assert.Empty(host.RequestedScopes);
        Assert.Equal(["0"], await QueryAsync(connectionString, $"SELECT count(*)::text FROM companies WHERE id = '{company.Id}'"));
    }

    private static (Company Company, UserAccount Manager) NewRegistration(string companyName, string email)
    {
        var company = Company.Create(Guid.NewGuid(), companyName, Now);
        var manager = UserAccount.Create(Guid.NewGuid(), company.Id, email, "Ana Gómez", UserSeed.PasswordHash, UserRole.Manager, null);
        return (company, manager);
    }

    private async Task<string> MigratedDatabaseAsync()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var context = postgres.CreateContext(connectionString);
        await context.Database.MigrateAsync();
        return connectionString;
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

    /// <summary>Composición mínima real: contexto ambiental de compañía, fábrica de ámbitos que anota las peticiones y un contador de guardados.</summary>
    private sealed class StoreHost : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly SaveCounter _counter = new();
        private readonly List<Guid> _requested = [];

        public StoreHost(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddCompanyContext();
            services.AddDbContext<TimeTrackerDbContext>(options => options.UseNpgsql(connectionString).AddInterceptors(_counter));
            services.AddSingleton<CompanyScopeFactory>();
            services.AddSingleton<ICompanyScopeFactory>(provider => new RecordingFactory(provider.GetRequiredService<CompanyScopeFactory>(), _requested));
            services.AddSingleton<CompanyRegistrationStore>();
            _provider = services.BuildServiceProvider();
        }

        public ICompanyRegistrationStore Store => _provider.GetRequiredService<CompanyRegistrationStore>();

        public IReadOnlyList<Guid> RequestedScopes
        {
            get
            {
                lock (_requested)
                {
                    return [.. _requested];
                }
            }
        }

        public int Saves => _counter.Count;

        public void Dispose() => _provider.Dispose();
    }

    private sealed class RecordingFactory(ICompanyScopeFactory inner, List<Guid> requested) : ICompanyScopeFactory
    {
        public AsyncServiceScope CreateScope(Guid companyId)
        {
            lock (requested)
            {
                requested.Add(companyId);
            }

            return inner.CreateScope(companyId);
        }
    }

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        private int _count;

        public int Count => _count;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
