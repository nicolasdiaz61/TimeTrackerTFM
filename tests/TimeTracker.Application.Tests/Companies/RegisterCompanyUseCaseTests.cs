using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Authentication;
using TimeTracker.Application.Companies;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests.Companies;

/// <summary>Autorregistro (DD-19): forma, código, campos, hash y almacén, sin fijar la compañía ni depender del correo.</summary>
public class RegisterCompanyUseCaseTests
{
    private const string InviteCode = "codigo-prueba-0123456789";
    private const string Password = "contrasena-prueba-12345";
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryCompanyRepository _companies = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeAccessTokenIssuer _issuer = new();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly FakeCompanyRegistrationStore _store;

    public RegisterCompanyUseCaseTests() => _store = new FakeCompanyRegistrationStore(_companies);

    private RegisterCompanyUseCase CreateUseCase(ICompanyRegistrationStore? store = null)
        => new(new InviteCodeValidator([InviteCode]), _hasher, store ?? _store, new SessionIssuer(_companies, _issuer), _clock);

    private static RegisterCompanyCommand Command(
        string? companyName = "  Acme   Norte ",
        string? managerName = " Ana Gómez ",
        string? email = " Ana@Example.test ",
        string? password = Password,
        string? inviteCode = InviteCode)
        => new(companyName, managerName, email, password, inviteCode);

    [Fact]
    public async Task Register_WithAValidBody_CreatesAnActiveCompanyWithoutPlatformAssistantAndItsManager()
    {
        var result = await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        var created = Assert.IsType<RegisterCompanyResult.Created>(result);
        var (company, manager) = Assert.Single(_store.Calls);
        Assert.Equal("Acme   Norte", company.Name);
        Assert.Equal("acme norte", company.NameNormalized);
        Assert.Equal(CompanyStatus.Active, company.Status);
        Assert.False(company.UsesPlatformAssistant);
        Assert.Equal(Now, company.CreatedAt);
        Assert.Equal(company.Id, manager.CompanyId);
        Assert.NotEqual(Guid.Empty, company.Id);
        Assert.NotEqual(company.Id, manager.Id);
        Assert.Equal(UserRole.Manager, manager.Role);
        Assert.Null(manager.ManagerId);
        Assert.False(manager.MustChangePassword);
        Assert.Equal("ana@example.test", manager.Email);
        Assert.Equal("Ana Gómez", manager.DisplayName);
        Assert.Equal(company.Id, created.CompanyId);
        Assert.Equal($"token-de-{manager.Id}", created.Session.Token.Value);
        Assert.Equal("Acme   Norte", created.Session.User.CompanyName);
        Assert.Equal("Acme   Norte", Assert.Single(_issuer.CompanyNames));
    }

    [Fact]
    public async Task Register_StoresOnlyTheHashOfThePasswordAndKeepsItsOuterSpaces()
    {
        const string WithSpaces = "  contrasena-con-espacios  ";

        await CreateUseCase().ExecuteAsync(Command(password: WithSpaces), CancellationToken.None);

        var manager = Assert.Single(_store.Calls).Manager;
        Assert.Equal(FakePasswordHasher.HashOf(WithSpaces), manager.PasswordHash);
        Assert.NotEqual(WithSpaces, manager.PasswordHash);
        Assert.Equal(1, _hasher.HashCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("codigo-prueba-0123456780")]
    public async Task Register_WithAnAbsentBlankOrWrongCode_ReturnsInvalidInviteCodeBeforeValidatingOrHashingAnything(string? code)
    {
        // Todos los campos son inválidos a propósito: el código se comprueba primero y no se revela nada más.
        var command = Command(companyName: " ", managerName: " ", email: "sin-arroba", password: "corta", inviteCode: code);

        var result = await CreateUseCase().ExecuteAsync(command, CancellationToken.None);

        Assert.IsType<RegisterCompanyResult.InvalidInviteCode>(result);
        Assert.Equal(0, _hasher.HashCalls);
        Assert.Empty(_store.Calls);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task Register_WithAWrongCodeAndAnAlreadyRegisteredEmail_ReturnsInvalidInviteCodeNotADuplicate()
    {
        _store.Outcome = RegistrationOutcome.DuplicateEmail;

        var result = await CreateUseCase().ExecuteAsync(Command(inviteCode: "codigo-incorrecto-9999"), CancellationToken.None);

        Assert.IsType<RegisterCompanyResult.InvalidInviteCode>(result);
        Assert.Empty(_store.Calls);
    }

    public static TheoryData<string?, string?, string?, string?, string> InvalidFields => new()
    {
        { " ", "Ana", "ana@example.test", Password, "companyName" },
        { null, "Ana", "ana@example.test", Password, "companyName" },
        { new string('c', 101), "Ana", "ana@example.test", Password, "companyName" },
        { "Acme", " ", "ana@example.test", Password, "managerName" },
        { "Acme", new string('n', 101), "ana@example.test", Password, "managerName" },
        { "Acme", "Ana", "sin-arroba", Password, "email" },
        { "Acme", "Ana", " ", Password, "email" },
        { "Acme", "Ana", null, Password, "email" },
        { "Acme", "Ana", "ana@example.test", "corta-123", "password" },
        { "Acme", "Ana", "ana@example.test", null, "password" },
        { "Acme", "Ana", "ana@example.test", new string('p', 129), "password" },
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Register_WithAnInvalidField_ReturnsThatFieldOnlyAndWritesAndHashesNothing(
        string? companyName, string? managerName, string? email, string? password, string expectedField)
    {
        var result = await CreateUseCase().ExecuteAsync(Command(companyName, managerName, email, password), CancellationToken.None);

        var invalid = Assert.IsType<RegisterCompanyResult.Invalid>(result);
        Assert.Equal(expectedField, Assert.Single(invalid.Errors).Field);
        Assert.Equal(0, _hasher.HashCalls);
        Assert.Empty(_store.Calls);
    }

    [Fact]
    public async Task Register_WithSeveralInvalidFields_ReportsAllOfThemWithoutEchoingThePassword()
    {
        var result = await CreateUseCase().ExecuteAsync(Command(" ", " ", "sin-arroba", "corta-123"), CancellationToken.None);

        var invalid = Assert.IsType<RegisterCompanyResult.Invalid>(result);
        Assert.Equal(["companyName", "managerName", "email", "password"], invalid.Errors.Select(error => error.Field));
        Assert.All(invalid.Errors, error => Assert.DoesNotContain("corta-123", error.Message, StringComparison.Ordinal));
        Assert.All(invalid.Errors, error => Assert.DoesNotContain(InviteCode, error.Message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Register_AcceptsNamesAtTheBoundaries(int length)
    {
        var result = await CreateUseCase().ExecuteAsync(
            Command(companyName: new string('c', length), managerName: new string('m', length)),
            CancellationToken.None);

        Assert.IsType<RegisterCompanyResult.Created>(result);
        Assert.Equal(length, Assert.Single(_store.Calls).Company.Name.Length);
    }

    [Fact]
    public async Task Register_WithADuplicateName_ReturnsDuplicateNameAndIssuesNoSession()
    {
        _store.Outcome = RegistrationOutcome.DuplicateName;

        var result = await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        Assert.IsType<RegisterCompanyResult.DuplicateName>(result);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_ReturnsADifferentResultThanADuplicateName()
    {
        _store.Outcome = RegistrationOutcome.DuplicateEmail;

        var result = await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        Assert.IsType<RegisterCompanyResult.DuplicateEmail>(result);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task Register_WhenTheStoreFails_PropagatesAndIssuesNoSession()
    {
        _store.Failure = new InvalidOperationException("fallo simulado al insertar el usuario");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateUseCase().ExecuteAsync(Command(), CancellationToken.None));

        Assert.Empty(_issuer.IssuedFor);
        Assert.Empty(_companies.LoginInfoRequests);
    }

    [Fact]
    public async Task Register_IssuesTheTokenThroughTheCommonIssuerThatChecksTheCompanyIsActive()
    {
        await CreateUseCase().ExecuteAsync(Command(), CancellationToken.None);

        var company = Assert.Single(_store.Calls).Company;
        Assert.Equal(company.Id, Assert.Single(_companies.LoginInfoRequests));
    }

    [Fact]
    public async Task Register_WhenTheCompanyIsNotActiveAfterCreation_FailsInsteadOfReturningASession()
    {
        // Imposible hoy (la compañía nace Active), pero un token jamás se emite saltándose la regla del emisor común.
        var useCase = CreateUseCase(new SuspendingStore(_companies));

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Empty(_issuer.IssuedFor);
    }

    private sealed class SuspendingStore(InMemoryCompanyRepository companies) : ICompanyRegistrationStore
    {
        public Task<RegistrationOutcome> CreateAsync(Company company, UserAccount manager, CancellationToken cancellationToken)
        {
            companies.Seed(company.Id, company.Name, CompanyStatus.Suspended);
            return Task.FromResult(RegistrationOutcome.Created);
        }
    }
}
