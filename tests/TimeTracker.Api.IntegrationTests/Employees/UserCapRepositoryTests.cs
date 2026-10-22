using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// V-14 (DD-21): el tope de personas por compañía es exacto bajo concurrencia. Se compara el mecanismo elegido (transacción
/// <c>Serializable</c> con reintento acotado, válida también entre instancias) con el recuento seguido de la inserción sin
/// transacción, que es el control que demuestra que la carrera existe. Un temporizador no sincroniza nada: una barrera asíncrona
/// hace que las cinco altas lean el recuento antes de que ninguna escriba.
/// </summary>
[Collection("Postgres")]
public class UserCapRepositoryTests(PostgresFixture postgres)
{
    private const int Contenders = 5;

    [Fact]
    public async Task FiveSimultaneousAddsAtTheLastSlot_CreateExactlyOne_AndTheRestSeeTheCap()
    {
        var (companyId, managerId) = await SeedCompanyAsync(users: 2);
        var barrier = new AsyncBarrier(Contenders);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Contenders).Select(async index =>
        {
            await using var context = ContextWith(companyId, new CountBarrierInterceptor(barrier));
            return await new UserRepository(context).AddWithinCapAsync(NewUser(companyId, managerId, $"alta{index}"), maxUsers: 3, CancellationToken.None);
        }));

        Assert.Equal(1, outcomes.Count(outcome => outcome == AddUserOutcome.Created));
        Assert.Equal(Contenders - 1, outcomes.Count(outcome => outcome == AddUserOutcome.CapReached));
        Assert.Equal(3, await CountAsync(companyId));
        // La carrera fue real: las cinco leyeron el recuento antes de escribir y los perdedores tuvieron que reintentar.
        Assert.True(barrier.Arrivals == Contenders, "Las cinco altas debían leer el recuento antes de que ninguna escribiera.");
        Assert.True(barrier.CountQueries > Contenders, "Los perdedores debían haber reintentado tras un conflicto de serialización.");
    }

    [Fact]
    public async Task Control_TheSameRaceWithoutASerializableTransaction_ExceedsTheCap()
    {
        var (companyId, managerId) = await SeedCompanyAsync(users: 2);
        var barrier = new AsyncBarrier(Contenders);

        await Task.WhenAll(Enumerable.Range(0, Contenders).Select(async index =>
        {
            await using var context = ContextWith(companyId, new CountBarrierInterceptor(barrier));
            if (await context.Users.CountAsync(CancellationToken.None) < 3)
            {
                context.Users.Add(NewUser(companyId, managerId, $"control{index}"));
                await context.SaveChangesAsync(CancellationToken.None);
            }
        }));

        Assert.Equal(2 + Contenders, await CountAsync(companyId));
    }

    [Fact]
    public async Task BelowTheCap_TheUserIsCreated_AndTheCountOfAnotherCompanyDoesNotMatter()
    {
        var (full, fullManager) = await SeedCompanyAsync(users: 3);
        var (roomy, roomyManager) = await SeedCompanyAsync(users: 1);

        var atTheCap = await AddAsync(full, NewUser(full, fullManager, "lleno"), maxUsers: 3);
        var belowTheCap = await AddAsync(roomy, NewUser(roomy, roomyManager, "libre"), maxUsers: 3);

        Assert.Equal(AddUserOutcome.CapReached, atTheCap);
        Assert.Equal(AddUserOutcome.Created, belowTheCap);
        Assert.Equal(3, await CountAsync(full));
        Assert.Equal(2, await CountAsync(roomy));
    }

    [Fact]
    public async Task AnEmailThatExistsInAnotherCompany_ReturnsDuplicateEmail_AndAddsNothing()
    {
        var (companyId, managerId) = await SeedCompanyAsync(users: 1);
        var (otherId, otherManager) = await SeedCompanyAsync(users: 1);
        var existing = NewUser(otherId, otherManager, "existente");
        await UserSeed.InsertRawAsync(postgres.ConnectionString, existing.Id, existing.Email, companyId: otherId);

        var outcome = await AddAsync(companyId, UserAccount.Create(Guid.NewGuid(), companyId, existing.Email, "Persona", UserSeed.PasswordHash, UserRole.Employee, managerId), maxUsers: 50);

        Assert.Equal(AddUserOutcome.DuplicateEmail, outcome);
        Assert.Equal(1, await CountAsync(companyId));
    }

    [Fact]
    public async Task AResponsibleOfAnotherCompany_IsRejectedByTheCompositeForeignKey_AndAddsNothing()
    {
        var (companyId, _) = await SeedCompanyAsync(users: 1);
        var (_, foreignManager) = await SeedCompanyAsync(users: 1);

        var outcome = await AddAsync(companyId, NewUser(companyId, foreignManager, "huerfano"), maxUsers: 50);

        Assert.Equal(AddUserOutcome.InvalidManager, outcome);
        Assert.Equal(1, await CountAsync(companyId));
    }

    [Fact]
    public async Task WhenEverySaveFailsWithASerializationFailure_TheRetriesAreBoundedToThreeAndTheResultIsBusy()
    {
        var (companyId, managerId) = await SeedCompanyAsync(users: 1);
        var failing = new SerializationFailureInterceptor();
        await using var context = ContextWith(companyId, failing);

        var outcome = await new UserRepository(context).AddWithinCapAsync(NewUser(companyId, managerId, "ocupado"), maxUsers: 50, CancellationToken.None);

        Assert.Equal(AddUserOutcome.Busy, outcome);
        Assert.Equal(4, failing.Attempts);
        Assert.Equal(1, await CountAsync(companyId));
    }

    [Fact]
    public async Task ASerializationFailureThatClears_OnTheSecondAttempt_ReturnsCreated()
    {
        var (companyId, managerId) = await SeedCompanyAsync(users: 1);
        var failing = new SerializationFailureInterceptor(failures: 1);
        await using var context = ContextWith(companyId, failing);

        var outcome = await new UserRepository(context).AddWithinCapAsync(NewUser(companyId, managerId, "reintento"), maxUsers: 50, CancellationToken.None);

        Assert.Equal(AddUserOutcome.Created, outcome);
        Assert.Equal(2, failing.Attempts);
        Assert.Equal(2, await CountAsync(companyId));
    }

    [Fact]
    public async Task ASerializationFailureWrappedInTwoLevelsByEf_IsRetriedToo_NotTurnedIntoAnError()
    {
        var (companyId, managerId) = await SeedCompanyAsync(users: 1);
        var failing = new SerializationFailureInterceptor(failures: 1, wrappedLikeACommitFailure: true);
        await using var context = ContextWith(companyId, failing);

        var outcome = await new UserRepository(context).AddWithinCapAsync(NewUser(companyId, managerId, "envuelto"), maxUsers: 50, CancellationToken.None);

        Assert.Equal(AddUserOutcome.Created, outcome);
        Assert.Equal(2, failing.Attempts);
        Assert.Equal(2, await CountAsync(companyId));
    }

    private async Task<AddUserOutcome> AddAsync(Guid companyId, UserAccount user, int maxUsers)
    {
        await using var context = postgres.CreateContext(postgres.ConnectionString, companyId);
        return await new UserRepository(context).AddWithinCapAsync(user, maxUsers, CancellationToken.None);
    }

    private TimeTrackerDbContext ContextWith(Guid companyId, IInterceptor interceptor)
        => new(
            new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(postgres.ConnectionString).AddInterceptors(interceptor).Options,
            new TestCompanyContext(companyId));

    private async Task<(Guid CompanyId, Guid ManagerId)> SeedCompanyAsync(int users)
    {
        var companyId = await CompanySeed.InsertAsync(postgres.ConnectionString);
        var managerId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, managerId, $"responsable.{managerId:N}@example.test", "Manager", companyId: companyId);
        for (var index = 1; index < users; index++)
        {
            var id = Guid.NewGuid();
            await UserSeed.InsertRawAsync(postgres.ConnectionString, id, $"empleado.{id:N}@example.test", managerId: managerId, companyId: companyId);
        }

        return (companyId, managerId);
    }

    private static UserAccount NewUser(Guid companyId, Guid managerId, string name)
        => UserAccount.Create(Guid.NewGuid(), companyId, $"{name}.{Guid.NewGuid():N}@example.test", "Persona de prueba", UserSeed.PasswordHash, UserRole.Employee, managerId, mustChangePassword: true);

    private async Task<long> CountAsync(Guid companyId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM users WHERE company_id = @company", connection);
        command.Parameters.AddWithValue("company", companyId);
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }

    /// <summary>Barrera asíncrona: libera a todos cuando llega el último participante (o falla si alguno no llega).</summary>
    private sealed class AsyncBarrier(int participants)
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        private int _countQueries;

        public int Arrivals => Volatile.Read(ref _arrivals);

        public int CountQueries => Volatile.Read(ref _countQueries);

        public void CountQueryExecuted() => Interlocked.Increment(ref _countQueries);

        public async Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == participants)
            {
                _released.TrySetResult();
            }

            await _released.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    /// <summary>Tras el primer recuento de cada contexto espera a que todos los demás lo hayan leído también.</summary>
    private sealed class CountBarrierInterceptor(AsyncBarrier barrier) : DbCommandInterceptor
    {
        private bool _waited;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("count(*)", StringComparison.OrdinalIgnoreCase))
            {
                barrier.CountQueryExecuted();
                if (!_waited)
                {
                    _waited = true;
                    await barrier.ArriveAsync();
                }
            }

            return result;
        }
    }

    /// <summary>Hace fallar los primeros guardados con un conflicto de serialización (40001); sin límite, todos.</summary>
    private sealed class SerializationFailureInterceptor(int failures = int.MaxValue, bool wrappedLikeACommitFailure = false) : SaveChangesInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts <= failures)
            {
                var serialization = new PostgresException("could not serialize access", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
                var saving = new DbUpdateException("Conflicto de serialización simulado.", serialization);
                // Con altas simultáneas reales EF entrega el fallo envuelto en dos niveles (InvalidOperationException «fallo transitorio» sobre
                // DbUpdateException sobre PostgresException), como se observó en la prueba de cinco altas a la vez.
                throw wrappedLikeACommitFailure
                    ? new InvalidOperationException("An exception has been raised that is likely due to a transient failure.", saving)
                    : saving;
            }

            return ValueTask.FromResult(result);
        }
    }
}
