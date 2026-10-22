using TimeTracker.Application.Authentication;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests;

/// <summary>
/// Colaborador único que emite sesiones: comprueba que la compañía del usuario está <c>Active</c> antes de emitir el token (DD-6),
/// de modo que el inicio de sesión, el cambio de contraseña y el registro no puedan saltarse la regla.
/// </summary>
public class SessionIssuerTests
{
    private static readonly Guid AcmeId = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");
    private static readonly Guid LuisId = Guid.Parse("0197a1b2-3c3d-7e4f-8a5b-6c7d8e9f0a3d");

    private readonly InMemoryCompanyRepository _companies = new();
    private readonly FakeAccessTokenIssuer _issuer = new();

    private SessionIssuer CreateIssuer() => new(_companies, _issuer);

    private static UserAccount User(Guid companyId, bool mustChangePassword = false)
        => UserAccount.Create(LuisId, companyId, "luis.empleado@example.test", "Luis Empleado", "hash", UserRole.Employee, null, mustChangePassword);

    [Fact]
    public async Task IssueAsync_WithAnActiveCompany_IssuesTheTokenWithTheCompanyNameAndBuildsTheSummary()
    {
        _companies.Seed(AcmeId, "Acme Norte");
        var user = User(AcmeId);

        var result = await CreateIssuer().IssueAsync(user, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal($"token-de-{LuisId}", result.Token.Value);
        Assert.Equal(new UserSummary(LuisId, "luis.empleado@example.test", "Luis Empleado", UserRole.Employee, "Acme Norte", MustChangePassword: false), result.User);
        Assert.Same(user, Assert.Single(_issuer.IssuedFor));
        Assert.Equal("Acme Norte", Assert.Single(_issuer.CompanyNames));
        Assert.Equal(AcmeId, Assert.Single(_companies.LoginInfoRequests));
    }

    [Fact]
    public async Task IssueAsync_WithAPendingPasswordChange_ReportsItInTheSummary()
    {
        _companies.Seed(AcmeId, "Acme Norte");

        var result = await CreateIssuer().IssueAsync(User(AcmeId, mustChangePassword: true), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.User.MustChangePassword);
    }

    [Fact]
    public async Task IssueAsync_WithASuspendedCompany_ReturnsNullAndIssuesNothing()
    {
        _companies.Seed(AcmeId, "Acme Norte", CompanyStatus.Suspended);

        var result = await CreateIssuer().IssueAsync(User(AcmeId), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(_issuer.IssuedFor);
        Assert.Equal(AcmeId, Assert.Single(_companies.LoginInfoRequests));
    }

    [Fact]
    public async Task IsCompanyActiveAsync_IsTrueOnlyForAnExistingActiveCompany_AndIssuesNothing()
    {
        _companies.Seed(AcmeId, "Acme Norte");
        var suspendedId = Guid.NewGuid();
        _companies.Seed(suspendedId, "Suspendida", CompanyStatus.Suspended);
        var issuer = CreateIssuer();

        Assert.True(await issuer.IsCompanyActiveAsync(AcmeId, CancellationToken.None));
        Assert.False(await issuer.IsCompanyActiveAsync(suspendedId, CancellationToken.None));
        Assert.False(await issuer.IsCompanyActiveAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(_issuer.IssuedFor);
    }

    [Fact]
    public async Task IssueAsync_WithAMissingCompany_ReturnsNullAndIssuesNothing()
    {
        var result = await CreateIssuer().IssueAsync(User(AcmeId), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(_issuer.IssuedFor);
    }
}
