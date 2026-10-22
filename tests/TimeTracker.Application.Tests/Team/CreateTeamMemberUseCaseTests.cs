using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Team;

/// <summary>Alta de personas por un Manager (DD-21, employee-management): compañía y responsable del token, contraseña temporal y tope.</summary>
public class CreateTeamMemberUseCaseTests
{
    private static readonly Guid AcmeId = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");
    private static readonly Guid MartaId = Guid.Parse("0197a1b2-6c3d-7e4f-8a5b-6c7d8e9f0a60");
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly SequenceTemporaryPasswordGenerator _generator = new();
    private readonly FakeTimeProvider _clock = new(Now);

    private CreateTeamMemberUseCase CreateUseCase(int maxEmployees = 50)
        => new(_users, new FixedCurrentUser(MartaId, UserRole.Manager, AcmeId), _hasher, _generator, new FixedCompanyLimits(maxEmployees), _clock);

    private static CreateTeamMemberCommand Command(
        string? displayName = " Luis Empleado ",
        string? email = " Luis@Example.test ",
        string? role = null,
        Guid? managerId = null)
        => new(displayName, email, role, managerId);

    private static UserAccount User(Guid id, UserRole role, Guid? companyId = null, string? email = null)
        => UserAccount.Create(id, companyId ?? AcmeId, email ?? $"{id:N}@example.test", "Persona de prueba", "hash-de-prueba", role, null);

    [Fact]
    public async Task Create_WithOnlyNameAndEmail_CreatesAnEmployeeOfTheCurrentCompanyUnderTheCreatorWithAPendingPasswordChange()
    {
        var result = await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        var created = Assert.IsType<CreateTeamMemberResult.Created>(result);
        var (user, maxUsers) = Assert.Single(_users.AddWithinCapCalls);
        Assert.Same(user, created.User);
        Assert.Equal(50, maxUsers);
        Assert.Equal(AcmeId, user.CompanyId);
        Assert.Equal(UserRole.Employee, user.Role);
        Assert.Equal(MartaId, user.ManagerId);
        Assert.True(user.MustChangePassword);
        Assert.Equal("luis@example.test", user.Email);
        Assert.Equal("Luis Empleado", user.DisplayName);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public async Task Create_ReturnsTheTemporaryPasswordOnceAndStoresOnlyItsHash()
    {
        var result = await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        var created = Assert.IsType<CreateTeamMemberResult.Created>(result);
        Assert.Equal("temporal-prueba-0001", created.TemporaryPassword);
        Assert.Equal(FakePasswordHasher.HashOf("temporal-prueba-0001"), created.User.PasswordHash);
        Assert.NotEqual(created.TemporaryPassword, created.User.PasswordHash);
        Assert.Equal(1, _generator.Calls);
        Assert.Equal(1, _hasher.HashCalls);
    }

    [Fact]
    public async Task Create_WithTwoCalls_GeneratesADifferentTemporaryPasswordEachTime()
    {
        var useCase = CreateUseCase();

        var first = Assert.IsType<CreateTeamMemberResult.Created>(await useCase.ExecuteAsync(Command(email: "uno@example.test"), CancellationToken.None));
        var second = Assert.IsType<CreateTeamMemberResult.Created>(await useCase.ExecuteAsync(Command(email: "dos@example.test"), CancellationToken.None));

        Assert.NotEqual(first.TemporaryPassword, second.TemporaryPassword);
        Assert.NotEqual(first.User.PasswordHash, second.User.PasswordHash);
    }

    [Theory]
    [InlineData("Employee", UserRole.Employee)]
    [InlineData("Manager", UserRole.Manager)]
    [InlineData("manager", UserRole.Manager)]
    [InlineData(" EMPLOYEE ", UserRole.Employee)]
    public async Task Create_WithAKnownRole_CreatesThatRole_AndANewManagerAlsoReportsToTheCreator(string role, UserRole expected)
    {
        var result = await CreateUseCase().ExecuteAsync(Command(role: role), CancellationToken.None);

        var created = Assert.IsType<CreateTeamMemberResult.Created>(result);
        Assert.Equal(expected, created.User.Role);
        Assert.Equal(MartaId, created.User.ManagerId);
    }

    [Fact]
    public async Task Create_WithAManagerOfTheSameCompany_UsesItAsTheResponsible()
    {
        var otherManager = User(Guid.NewGuid(), UserRole.Manager);
        _users.Seed(otherManager);

        var result = await CreateUseCase().ExecuteAsync(Command(managerId: otherManager.Id), CancellationToken.None);

        Assert.Equal(otherManager.Id, Assert.IsType<CreateTeamMemberResult.Created>(result).User.ManagerId);
    }

    [Fact]
    public async Task Create_WithAnUnknownResponsible_AndWithOneThatIsNotAManager_ReturnTheSameErrorOnManagerId()
    {
        // El repositorio solo ve la compañía del contexto: un responsable de otra compañía es, para él, un inexistente.
        var employee = User(Guid.NewGuid(), UserRole.Employee);
        _users.Seed(employee);
        var useCase = CreateUseCase();

        var unknown = await useCase.ExecuteAsync(Command(managerId: Guid.NewGuid()), CancellationToken.None);
        var notAManager = await useCase.ExecuteAsync(Command(managerId: employee.Id), CancellationToken.None);

        List<FieldError> expected = [new("managerId", "El responsable debe ser un Manager de su compañía.")];
        Assert.Equal(expected, Assert.IsType<CreateTeamMemberResult.Invalid>(unknown).Errors);
        Assert.Equal(expected, Assert.IsType<CreateTeamMemberResult.Invalid>(notAManager).Errors);
        Assert.Empty(_users.AddWithinCapCalls);
        Assert.Equal(0, _hasher.HashCalls);
    }

    [Fact]
    public async Task Create_WhenTheStoreRejectsTheResponsible_ReturnsTheSameErrorOnManagerId()
    {
        // Red de seguridad: si la clave foránea compuesta lo rechaza, el resultado es el mismo que el de un inexistente.
        _users.ForcedOutcome = AddUserOutcome.InvalidManager;

        var result = await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        Assert.Equal(
            [new FieldError("managerId", "El responsable debe ser un Manager de su compañía.")],
            Assert.IsType<CreateTeamMemberResult.Invalid>(result).Errors);
    }

    public static TheoryData<string?, string?, string?, string> InvalidFields => new()
    {
        { " ", "luis@example.test", null, "displayName" },
        { null, "luis@example.test", null, "displayName" },
        { new string('n', 101), "luis@example.test", null, "displayName" },
        { "Luis", "sin-arroba", null, "email" },
        { "Luis", " ", null, "email" },
        { "Luis", null, null, "email" },
        { "Luis", "luis@example.test", "Admin", "role" },
        { "Luis", "luis@example.test", "", "role" },
        { "Luis", "luis@example.test", "1", "role" },
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Create_WithAnInvalidField_ReturnsOnlyThatFieldErrorAndCreatesNothing(string? name, string? email, string? role, string field)
    {
        var result = await CreateUseCase().ExecuteAsync(new CreateTeamMemberCommand(name, email, role, null), CancellationToken.None);

        var invalid = Assert.IsType<CreateTeamMemberResult.Invalid>(result);
        Assert.Equal([field], invalid.Errors.Select(error => error.Field));
        Assert.Empty(_users.AddWithinCapCalls);
        Assert.Equal(0, _hasher.HashCalls);
        Assert.Equal(0, _generator.Calls);
    }

    [Fact]
    public async Task Create_WithSeveralInvalidFields_ReportsEachOne()
    {
        var result = await CreateUseCase().ExecuteAsync(new CreateTeamMemberCommand(" ", "sin-arroba", "Admin", null), CancellationToken.None);

        Assert.Equal(["displayName", "email", "role"], Assert.IsType<CreateTeamMemberResult.Invalid>(result).Errors.Select(error => error.Field));
    }

    [Fact]
    public async Task Create_WithAnEmailThatAlreadyExists_ReturnsDuplicateEmail()
    {
        _users.Seed(User(Guid.NewGuid(), UserRole.Employee, companyId: Guid.NewGuid(), email: "luis@example.test"));

        var result = await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        Assert.IsType<CreateTeamMemberResult.DuplicateEmail>(result);
    }

    [Fact]
    public async Task Create_AtTheCap_ReturnsCapReached_DistinctFromADuplicateEmail()
    {
        _users.Seed(User(Guid.NewGuid(), UserRole.Employee), User(Guid.NewGuid(), UserRole.Employee), User(Guid.NewGuid(), UserRole.Employee));

        var atTheCap = await CreateUseCase(maxEmployees: 3).ExecuteAsync(Command(), CancellationToken.None);
        var belowTheCap = await CreateUseCase(maxEmployees: 4).ExecuteAsync(Command(), CancellationToken.None);

        Assert.IsType<CreateTeamMemberResult.CapReached>(atTheCap);
        Assert.IsType<CreateTeamMemberResult.Created>(belowTheCap);
    }

    [Fact]
    public async Task Create_WhenTheStoreCannotResolveTheContention_ReturnsBusy()
    {
        _users.ForcedOutcome = AddUserOutcome.Busy;

        var result = await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        Assert.IsType<CreateTeamMemberResult.Busy>(result);
    }
}
