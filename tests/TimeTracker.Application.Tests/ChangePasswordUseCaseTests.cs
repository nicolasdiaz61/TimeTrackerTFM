using TimeTracker.Application.Authentication;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests;

/// <summary>
/// Cambio de contraseña (DD-21): la compañía activa se comprueba antes de cambiar nada, la actual se verifica con el hasher, la
/// nueva cumple la política y el guardado respeta el token de concurrencia de la fila.
/// </summary>
public class ChangePasswordUseCaseTests
{
    private static readonly Guid AcmeId = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");
    private static readonly Guid MartaId = Guid.Parse("0197a1b2-6c3d-7e4f-8a5b-6c7d8e9f0a60");
    private const string Temporary = "temporal-prueba-0001";
    private const string NewPassword = "contrasena-prueba-12345";

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryCompanyRepository _companies = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeAccessTokenIssuer _issuer = new();
    private readonly UserAccount _luis;

    public ChangePasswordUseCaseTests()
    {
        _companies.Seed(AcmeId, "Acme Norte");
        _luis = UserAccount.Create(
            Guid.NewGuid(), AcmeId, "luis.empleado@example.test", "Luis Empleado", FakePasswordHasher.HashOf(Temporary), UserRole.Employee, MartaId, mustChangePassword: true);
        _users.Seed(_luis);
    }

    private ChangePasswordUseCase CreateUseCase(Guid? currentUserId = null)
        => new(_users, new FixedCurrentUser(currentUserId ?? _luis.Id, UserRole.Employee, AcmeId), _hasher, new SessionIssuer(_companies, _issuer));

    [Fact]
    public async Task Change_WithTheRightCurrentPasswordAndAValidNewOne_ReplacesTheHashClearsTheMarkAndIssuesAToken()
    {
        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand(Temporary, NewPassword), CancellationToken.None);

        var changed = Assert.IsType<ChangePasswordResult.Changed>(result);
        Assert.Equal(FakePasswordHasher.HashOf(NewPassword), _luis.PasswordHash);
        Assert.False(_luis.MustChangePassword);
        Assert.Equal(1, _users.SaveChangesCalls);
        Assert.Equal($"token-de-{_luis.Id}", changed.Session.Token.Value);
        Assert.False(changed.Session.User.MustChangePassword);
        Assert.Equal("Acme Norte", changed.Session.User.CompanyName);
        Assert.False(_hasher.Verify(_luis.PasswordHash, Temporary), "La contraseña temporal debe dejar de ser válida.");
    }

    [Fact]
    public async Task Change_WithASuspendedCompany_ReturnsUnauthorizedBeforeVerifyingOrChangingAnything()
    {
        _companies.Seed(AcmeId, "Acme Norte", CompanyStatus.Suspended);

        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand(Temporary, NewPassword), CancellationToken.None);

        Assert.IsType<ChangePasswordResult.Unauthorized>(result);
        Assert.Equal(FakePasswordHasher.HashOf(Temporary), _luis.PasswordHash);
        Assert.True(_luis.MustChangePassword);
        Assert.Equal(0, _hasher.VerifyCalls);
        Assert.Equal(0, _hasher.HashCalls);
        Assert.Equal(0, _users.SaveChangesCalls);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task Change_ForAnUnknownUser_ReturnsUnauthorized()
    {
        var result = await CreateUseCase(currentUserId: Guid.NewGuid()).ExecuteAsync(new ChangePasswordCommand(Temporary, NewPassword), CancellationToken.None);

        Assert.IsType<ChangePasswordResult.Unauthorized>(result);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task Change_WithAWrongCurrentPassword_ReturnsAnErrorOnCurrentPasswordAndChangesNothing()
    {
        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand("no-es-la-actual-123", NewPassword), CancellationToken.None);

        var invalid = Assert.IsType<ChangePasswordResult.Invalid>(result);
        Assert.Equal(["currentPassword"], invalid.Errors.Select(error => error.Field));
        Assert.DoesNotContain("no-es-la-actual-123", invalid.Errors.Single().Message, StringComparison.Ordinal);
        Assert.Equal(FakePasswordHasher.HashOf(Temporary), _luis.PasswordHash);
        Assert.True(_luis.MustChangePassword);
        Assert.Equal(1, _hasher.VerifyCalls);
        Assert.Equal(0, _users.SaveChangesCalls);
        Assert.Empty(_issuer.IssuedFor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Change_WithAnEmptyCurrentPassword_ReturnsAnErrorOnCurrentPasswordWithoutHashing(string? current)
    {
        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand(current, NewPassword), CancellationToken.None);

        Assert.Equal(["currentPassword"], Assert.IsType<ChangePasswordResult.Invalid>(result).Errors.Select(error => error.Field));
        Assert.Equal(0, _hasher.VerifyCalls);
    }

    [Fact]
    public async Task Change_WithAnOversizedCurrentPassword_IsRejectedWithoutHashingIt()
    {
        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand(new string('x', LoginUseCase.MaxPasswordLength + 1), NewPassword), CancellationToken.None);

        Assert.Equal(["currentPassword"], Assert.IsType<ChangePasswordResult.Invalid>(result).Errors.Select(error => error.Field));
        Assert.Equal(0, _hasher.VerifyCalls);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(129)]
    public async Task Change_WithANewPasswordOutOfTheLengthLimits_ReturnsAnErrorOnNewPasswordAndChangesNothing(int length)
    {
        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand(Temporary, new string('n', length)), CancellationToken.None);

        var invalid = Assert.IsType<ChangePasswordResult.Invalid>(result);
        Assert.Equal(["newPassword"], invalid.Errors.Select(error => error.Field));
        Assert.Equal(FakePasswordHasher.HashOf(Temporary), _luis.PasswordHash);
        Assert.Equal(0, _users.SaveChangesCalls);
    }

    [Fact]
    public async Task Change_WithANewPasswordEqualToTheCurrentOne_ReturnsAnErrorOnNewPassword()
    {
        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand(Temporary, Temporary), CancellationToken.None);

        var invalid = Assert.IsType<ChangePasswordResult.Invalid>(result);
        Assert.Equal(["newPassword"], invalid.Errors.Select(error => error.Field));
        Assert.True(_luis.MustChangePassword);
        Assert.Equal(0, _users.SaveChangesCalls);
    }

    [Fact]
    public async Task Change_WithBothPasswordsWrong_ReportsBothFields()
    {
        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand("no-es-la-actual-123", "corta"), CancellationToken.None);

        Assert.Equal(["currentPassword", "newPassword"], Assert.IsType<ChangePasswordResult.Invalid>(result).Errors.Select(error => error.Field));
    }

    [Fact]
    public async Task Change_WhenTheRowChangedBetweenReadAndSave_ReturnsConflictAndIssuesNoToken()
    {
        _users.SaveFailure = new ConcurrentUserChangeException();

        var result = await CreateUseCase().ExecuteAsync(new ChangePasswordCommand(Temporary, NewPassword), CancellationToken.None);

        Assert.IsType<ChangePasswordResult.Conflict>(result);
        Assert.Equal(1, _users.SaveChangesCalls);
        Assert.Empty(_issuer.IssuedFor);
    }
}
