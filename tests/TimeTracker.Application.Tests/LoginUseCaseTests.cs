using TimeTracker.Application.Authentication;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests;

public class LoginUseCaseTests
{
    private const string Password = "clave-de-prueba-123";
    private static readonly Guid LuisId = Guid.Parse("0197a1b2-3c3d-7e4f-8a5b-6c7d8e9f0a3d");

    private readonly InMemoryUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeAccessTokenIssuer _issuer = new();
    private readonly InMemoryCompanyRepository _companies = new();
    private readonly UserAccount _luis;

    public LoginUseCaseTests()
    {
        _luis = UserAccount.Create(LuisId, Company.DefaultId, "luis.empleado@example.test", "Luis Empleado", _hasher.Hash(Password), UserRole.Employee, null);
        _users.Seed(_luis);
        _companies.Seed(Company.DefaultId, "Demo");
    }

    private LoginUseCase CreateUseCase() => new(_users, _hasher, new SessionIssuer(_companies, _issuer));

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndSummary()
    {
        var result = await CreateUseCase().ExecuteAsync(new LoginCommand("luis.empleado@example.test", Password), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal($"token-de-{LuisId}", result.Token.Value);
        Assert.Equal(FakeAccessTokenIssuer.ExpiresAt, result.Token.ExpiresAt);
        Assert.Equal(new UserSummary(LuisId, "luis.empleado@example.test", "Luis Empleado", UserRole.Employee, "Demo", MustChangePassword: false), result.User);
    }

    [Fact]
    public async Task Login_PassesTheFoundUserToTheIssuer()
    {
        await CreateUseCase().ExecuteAsync(new LoginCommand("luis.empleado@example.test", Password), CancellationToken.None);

        Assert.Same(_luis, Assert.Single(_issuer.IssuedFor));
    }

    [Fact]
    public async Task Login_WithASuspendedCompany_ReturnsNullAfterVerifyingThePasswordAndIssuesNothing()
    {
        _companies.Seed(Company.DefaultId, "Demo", CompanyStatus.Suspended);

        var result = await CreateUseCase().ExecuteAsync(new LoginCommand("luis.empleado@example.test", Password), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, _hasher.VerifyCalls);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task Login_WithAnActiveCompany_ReadsTheCompanyOfTheUserAndPutsItsNameInTheToken()
    {
        var acmeId = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");
        _companies.Seed(acmeId, "Acme Norte");
        _users.Seed(UserAccount.Create(Guid.Parse("0197a1b2-4c3d-7e4f-8a5b-6c7d8e9f0a4e"), acmeId, "ana@example.test", "Ana Responsable", _hasher.Hash(Password), UserRole.Manager, null, mustChangePassword: true));

        var result = await CreateUseCase().ExecuteAsync(new LoginCommand("ana@example.test", Password), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(("Acme Norte", true), (result.User.CompanyName, result.User.MustChangePassword));
        Assert.Equal("Acme Norte", Assert.Single(_issuer.CompanyNames));
        Assert.Equal(acmeId, Assert.Single(_companies.LoginInfoRequests));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsNullAndIssuesNothing()
    {
        var result = await CreateUseCase().ExecuteAsync(new LoginCommand("luis.empleado@example.test", "otra-clave-456"), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task Login_VerifiesThePasswordWithoutTrimming()
    {
        var marta = UserAccount.Create(Guid.Parse("0197a1b2-4c3d-7e4f-8a5b-6c7d8e9f0a4e"), Company.DefaultId, "marta.responsable@example.test", "Marta Responsable", _hasher.Hash(" clave "), UserRole.Manager, null);
        _users.Seed(marta);

        var result = await CreateUseCase().ExecuteAsync(new LoginCommand("marta.responsable@example.test", " clave "), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(" clave ", _hasher.LastVerifiedPassword);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsNullAndVerifiesOnceWithNullHash()
    {
        var result = await CreateUseCase().ExecuteAsync(new LoginCommand("nadie@example.test", Password), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, _hasher.NullHashVerifications);
        Assert.Equal(1, _hasher.VerifyCalls);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task Login_NormalizesTheEmailBeforeLookup()
    {
        var result = await CreateUseCase().ExecuteAsync(new LoginCommand("  Luis.Empleado@Example.TEST ", Password), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("luis.empleado@example.test", _users.LastEmailRequested);
    }

    private static readonly string ValidEmail = "luis.empleado@example.test";
    private static readonly string LongEmail = new string('a', UserAccount.MaxEmailLength - "@example.test".Length + 1) + "@example.test";

    public static TheoryData<string?, string?> InvalidShapes => new()
    {
        { null, Password },
        { "", Password },
        { "   ", Password },
        { LongEmail, Password },
        { ValidEmail, null },
        { ValidEmail, "" },
        { ValidEmail, "   " },
        { ValidEmail, new string('p', LoginUseCase.MaxPasswordLength + 1) },
    };

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public async Task Login_WithInvalidShape_ReturnsNullWithoutQueryingOrVerifying(string? email, string? password)
    {
        var result = await CreateUseCase().ExecuteAsync(new LoginCommand(email, password), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _users.FindByEmailCalls);
        Assert.Equal(0, _hasher.VerifyCalls);
        Assert.Empty(_companies.LoginInfoRequests);
    }

    [Fact]
    public async Task Login_WithAShortPasswordFromTheOlderSystem_IsAcceptedBecauseThePasswordPolicyOnlyAppliesToNewPasswords()
    {
        // Una contraseña de 8 caracteres incumple la política de 12 (DD-20) pero un usuario previo debe poder entrar con ella.
        const string ShortPassword = "corta-08";
        var older = UserAccount.Create(Guid.NewGuid(), Company.DefaultId, "ana@example.test", "Ana", _hasher.Hash(ShortPassword), UserRole.Employee, null);
        _users.Seed(older);

        var result = await CreateUseCase().ExecuteAsync(new LoginCommand("ana@example.test", ShortPassword), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal($"token-de-{older.Id}", result.Token.Value);
        Assert.NotNull(PasswordPolicy.Validate(ShortPassword, "password"));
    }

    [Fact]
    public async Task Login_WithBoundaryLengths_IsAccepted()
    {
        var email = new string('b', UserAccount.MaxEmailLength - "@example.test".Length) + "@example.test";
        var password = new string('p', LoginUseCase.MaxPasswordLength);
        _users.Seed(UserAccount.Create(Guid.Parse("0197a1b2-5c3d-7e4f-8a5b-6c7d8e9f0a5f"), Company.DefaultId, email, "Usuario Limite", _hasher.Hash(password), UserRole.Employee, null));

        var result = await CreateUseCase().ExecuteAsync(new LoginCommand(email, password), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(254, email.Length);
        Assert.Equal(128, password.Length);
    }
}
