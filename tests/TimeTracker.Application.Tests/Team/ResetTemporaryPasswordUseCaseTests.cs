using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Team;

/// <summary>
/// Regenerar la contraseña temporal (DD-21): solo el responsable directo, solo antes del primer acceso y sin pisar un cambio
/// concurrente de la persona.
/// </summary>
public class ResetTemporaryPasswordUseCaseTests
{
    private static readonly Guid AcmeId = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");
    private static readonly Guid MartaId = Guid.Parse("0197a1b2-6c3d-7e4f-8a5b-6c7d8e9f0a60");
    private static readonly Guid OtherManagerId = Guid.Parse("0197a1b2-6c3d-7e4f-8a5b-6c7d8e9f0a61");
    private const string OldTemporary = "temporal-anterior-0000";

    private readonly InMemoryUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly SequenceTemporaryPasswordGenerator _generator = new();

    private ResetTemporaryPasswordUseCase CreateUseCase(Guid? currentUserId = null)
        => new(_users, new FixedCurrentUser(currentUserId ?? MartaId, UserRole.Manager, AcmeId), _hasher, _generator);

    private static UserAccount Report(Guid managerId, bool mustChangePassword = true)
        => UserAccount.Create(
            Guid.NewGuid(), AcmeId, $"{Guid.NewGuid():N}@example.test", "Luis Empleado", FakePasswordHasher.HashOf(OldTemporary), UserRole.Employee, managerId, mustChangePassword);

    [Fact]
    public async Task Reset_ForADirectReportWithAPendingChange_ReplacesTheHashAndKeepsTheMark()
    {
        var luis = Report(MartaId);
        _users.Seed(luis);

        var result = await CreateUseCase().ExecuteAsync(luis.Id, CancellationToken.None);

        var regenerated = Assert.IsType<ResetTemporaryPasswordResult.Regenerated>(result);
        Assert.Equal("temporal-prueba-0001", regenerated.TemporaryPassword);
        Assert.Equal(FakePasswordHasher.HashOf("temporal-prueba-0001"), luis.PasswordHash);
        Assert.True(luis.MustChangePassword);
        Assert.Equal(1, _users.SaveChangesCalls);
        Assert.False(_hasher.Verify(luis.PasswordHash, OldTemporary), "La contraseña anterior debe dejar de ser válida.");
        Assert.True(_hasher.Verify(luis.PasswordHash, regenerated.TemporaryPassword));
    }

    [Fact]
    public async Task Reset_ForAnotherManagersReport_AnUnknownMember_AndTheManagerHerself_AllReturnTheSameNotFound()
    {
        var ofOtherManager = Report(OtherManagerId);
        _users.Seed(ofOtherManager);
        var useCase = CreateUseCase();

        var results = new[]
        {
            await useCase.ExecuteAsync(ofOtherManager.Id, CancellationToken.None),
            await useCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None),
            await useCase.ExecuteAsync(MartaId, CancellationToken.None),
        };

        Assert.All(results, result => Assert.Equal(new ResetTemporaryPasswordResult.NotFound(), result));
        Assert.Equal(FakePasswordHasher.HashOf(OldTemporary), ofOtherManager.PasswordHash);
        Assert.Equal(0, _generator.Calls);
        Assert.Equal(0, _users.SaveChangesCalls);
    }

    [Fact]
    public async Task Reset_ForAPersonWhoAlreadyChangedThePassword_ReturnsConflictAndChangesNothing()
    {
        var luis = Report(MartaId, mustChangePassword: false);
        _users.Seed(luis);

        var result = await CreateUseCase().ExecuteAsync(luis.Id, CancellationToken.None);

        Assert.IsType<ResetTemporaryPasswordResult.Conflict>(result);
        Assert.Equal(FakePasswordHasher.HashOf(OldTemporary), luis.PasswordHash);
        Assert.Equal(0, _generator.Calls);
        Assert.Equal(0, _users.SaveChangesCalls);
    }

    [Fact]
    public async Task Reset_WhenTheRowChangedBetweenReadAndSave_ReturnsConflict()
    {
        var luis = Report(MartaId);
        _users.Seed(luis);
        _users.SaveFailure = new ConcurrentUserChangeException();

        var result = await CreateUseCase().ExecuteAsync(luis.Id, CancellationToken.None);

        Assert.IsType<ResetTemporaryPasswordResult.Conflict>(result);
        Assert.Equal(1, _users.SaveChangesCalls);
    }

    [Fact]
    public async Task Reset_WithTheRealGenerator_ProducesAPasswordThatMeetsThePolicy()
    {
        var luis = Report(MartaId);
        _users.Seed(luis);
        var useCase = new ResetTemporaryPasswordUseCase(
            _users, new FixedCurrentUser(MartaId, UserRole.Manager, AcmeId), _hasher, new TimeTracker.Application.Authentication.TemporaryPasswordGenerator());

        var result = await useCase.ExecuteAsync(luis.Id, CancellationToken.None);

        var regenerated = Assert.IsType<ResetTemporaryPasswordResult.Regenerated>(result);
        Assert.True(regenerated.TemporaryPassword.Length >= 16);
        Assert.Null(TimeTracker.Application.Authentication.PasswordPolicy.Validate(regenerated.TemporaryPassword, "temporaryPassword"));
    }
}
