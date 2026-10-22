using System.Net;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>TC-3 (DD-7): la guarda de escritura de <c>SaveChanges</c> rechaza toda entidad con una compañía distinta de la del contexto.</summary>
[Collection("Postgres")]
public class WriteGuardTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Add_OfAnEntityOfAnotherCompany_ThrowsAndWritesNothing()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var foreign = UserAccount.Create(Guid.NewGuid(), fixture.B.Id, $"nuevo.{Guid.NewGuid():N}@example.test", "Nuevo", UserSeed.PasswordHash, UserRole.Employee, managerId: null);
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);

        contextA.Users.Add(foreign);

        await Assert.ThrowsAsync<CompanyScopeViolationException>(() => contextA.SaveChangesAsync());
        Assert.Equal(["0"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT count(*)::text FROM users WHERE id = '{foreign.Id}'"));
    }

    [Fact]
    public async Task Add_OfAnEntityOfTheContextCompany_IsAllowed()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var own = UserAccount.Create(Guid.NewGuid(), fixture.A.Id, $"propio.{Guid.NewGuid():N}@example.test", "Propio", UserSeed.PasswordHash, UserRole.Employee, fixture.A.Manager.Id);
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);

        contextA.Users.Add(own);
        await contextA.SaveChangesAsync();

        Assert.Equal(["1"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT count(*)::text FROM users WHERE id = '{own.Id}'"));
    }

    [Fact]
    public void SynchronousSaveChanges_IsGuardedToo()
    {
        var company = Guid.NewGuid();
        using var context = postgres.CreateContext(postgres.ConnectionString, Company.DefaultId);
        context.TimeEntries.Add(TimeEntry.Start(company, Guid.NewGuid(), TimeEntryType.Work, EightAm));

        Assert.Throws<CompanyScopeViolationException>(() => context.SaveChanges());
    }

    [Fact]
    public async Task Update_OfADetachedEntityOfAnotherCompany_Throws()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        await using var contextB = postgres.CreateContext(fixture.ConnectionString, fixture.B.Id);
        var entryOfB = await contextB.TimeEntries.AsNoTracking().FirstAsync();
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);

        contextA.TimeEntries.Update(entryOfB);

        await Assert.ThrowsAsync<CompanyScopeViolationException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task Remove_OfAnEntityOfAnotherCompany_ThrowsAndTheRowRemains()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        await using var contextB = postgres.CreateContext(fixture.ConnectionString, fixture.B.Id);
        var entryOfB = await contextB.TimeEntries.AsNoTracking().FirstAsync();
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);

        contextA.TimeEntries.Remove(entryOfB);

        await Assert.ThrowsAsync<CompanyScopeViolationException>(() => contextA.SaveChangesAsync());
        Assert.Equal(["1"], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT count(*)::text FROM time_entries WHERE id = '{entryOfB.Id}'"));
    }

    [Fact]
    public async Task ChangingTheCompanyOfAnExistingEntity_Throws_EvenToTheCurrentContextCompany()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var company = new TestCompanyContext(fixture.A.Id);
        await using var context = new TimeTrackerDbContext(new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(fixture.ConnectionString).Options, company);
        var entry = await context.TimeEntries.FirstAsync();
        company.CompanyId = fixture.B.Id;

        // La nueva compañía coincide con la del contexto, pero la fila pertenecía a otra: cambiar la compañía nunca es una escritura legítima.
        context.Entry(entry).Property(nameof(TimeEntry.CompanyId)).CurrentValue = fixture.B.Id;

        await Assert.ThrowsAsync<CompanyScopeViolationException>(() => context.SaveChangesAsync());
        Assert.Equal([fixture.A.Id.ToString()], await MigrationDatabase.QueryAsync(fixture.ConnectionString, $"SELECT company_id::text FROM time_entries WHERE id = '{entry.Id}'"));
    }

    [Fact]
    public async Task ChangingTheCompanyOfAnExistingEntity_ToAnotherCompany_Throws()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);
        var entry = await contextA.TimeEntries.FirstAsync();

        contextA.Entry(entry).Property(nameof(TimeEntry.CompanyId)).CurrentValue = fixture.B.Id;

        await Assert.ThrowsAsync<CompanyScopeViolationException>(() => contextA.SaveChangesAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutACompanyOrWithAnEmptyOne_NoWriteIsAllowed(bool emptyGuid)
    {
        var entry = TimeEntry.Start(Company.DefaultId, Guid.NewGuid(), TimeEntryType.Work, EightAm);
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        await using var context = new TimeTrackerDbContext(options, new TestCompanyContext(emptyGuid ? Guid.Empty : null));

        context.TimeEntries.Add(entry);

        await Assert.ThrowsAsync<CompanyScopeViolationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TheViolationMessage_NamesNoPersonalDataNorIdentifiers()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        var foreign = UserAccount.Create(Guid.NewGuid(), fixture.B.Id, "ana.secreta@example.test", "Ana Secreta", UserSeed.PasswordHash, UserRole.Employee, managerId: null);
        await using var contextA = postgres.CreateContext(fixture.ConnectionString, fixture.A.Id);
        contextA.Users.Add(foreign);

        var exception = await Assert.ThrowsAsync<CompanyScopeViolationException>(() => contextA.SaveChangesAsync());

        var text = exception.ToString();
        Assert.DoesNotContain("ana.secreta", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana Secreta", text, StringComparison.Ordinal);
        Assert.DoesNotContain(foreign.Id.ToString(), text, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.B.Id.ToString(), text, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.A.Id.ToString(), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AViolationInARequest_Returns500FromTheGuardNotTheForeignKey_WithNoDataAndTheLogCarriesNoPersonalData()
    {
        using var employee = new PostgresApiFactory(postgres.ConnectionString);
        var foreignCompany = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var logs = new CapturingLoggerProvider();
        using var factory = employee.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ITimeEntryRepository>(provider => new ForeignWritingRepository(provider.GetRequiredService<TimeTrackerDbContext>(), foreignCompany));
            });
        });

        var response = await factory.CreateClient().PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(foreignCompany.ToString(), body, StringComparison.Ordinal);
        Assert.DoesNotContain(employee.EmployeeId.ToString(), body, StringComparison.Ordinal);
        // El 500 solo prueba la guarda si la causa es la guarda: sin ella, la clave foránea compuesta también daría 500. Por eso se
        // exige el tipo de excepción de la guarda en el registro y que la clave foránea no haya llegado a actuar.
        var log = new StringBuilder().AppendJoin('\n', logs.Messages).ToString();
        Assert.Contains(nameof(CompanyScopeViolationException), log, StringComparison.Ordinal);
        Assert.DoesNotContain("DbUpdateException", log, StringComparison.Ordinal);
        Assert.DoesNotContain("23503", log, StringComparison.Ordinal);
        Assert.Equal(["0"], await MigrationDatabase.QueryAsync(postgres.ConnectionString, $"SELECT count(*)::text FROM time_entries WHERE company_id = '{foreignCompany}'"));
        Assert.DoesNotContain(foreignCompany.ToString(), log, StringComparison.Ordinal);
        Assert.DoesNotContain($"{employee.EmployeeId:N}@example.test", log, StringComparison.Ordinal);
    }

    /// <summary>Doble que intenta escribir con una compañía distinta de la del contexto (un defecto que la guarda debe cortar).</summary>
    private sealed class ForeignWritingRepository(TimeTrackerDbContext context, Guid foreignCompany) : ITimeEntryRepository
    {
        public Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken) => Task.FromResult<TimeEntry?>(null);

        public Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TimeEntry>>([]);

        public Task<IReadOnlyList<TimeEntry>> ListInWindowAsync(Guid employeeId, TimeWindow window, int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TimeEntry>>([]);

        public async Task AddAsync(TimeEntry entry, CancellationToken cancellationToken)
        {
            context.TimeEntries.Add(TimeEntry.Start(foreignCompany, entry.EmployeeId, entry.Type, entry.ClockInAt));
            await context.SaveChangesAsync(cancellationToken);
        }

        public Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReplaceOpenAsync(TimeEntry closed, TimeEntry next, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
