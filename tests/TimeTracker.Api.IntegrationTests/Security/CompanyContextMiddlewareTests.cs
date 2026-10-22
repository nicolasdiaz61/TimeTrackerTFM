using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using TimeTracker.Api.Security;
using TimeTracker.Application.Identity;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Security;

public class CompanyContextMiddlewareTests
{
    private static readonly Guid CompanyA = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");

    [Fact]
    public async Task Invoke_WithAnAuthenticatedUser_SetsTheCompanyOfTheCurrentUserAndContinues()
    {
        var ambient = new AmbientCompanyContext();
        var continued = false;
        var middleware = new CompanyContextMiddleware(_ =>
        {
            continued = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) };

        await middleware.InvokeAsync(context, ambient, new StubCurrentUser(CompanyA));

        Assert.Equal(CompanyA, ambient.CompanyId);
        Assert.True(continued);
    }

    [Fact]
    public async Task Invoke_WithAnAnonymousRequest_LeavesNoCompanyWithoutReadingTheCurrentUser()
    {
        var ambient = new AmbientCompanyContext();
        var continued = false;
        var middleware = new CompanyContextMiddleware(_ =>
        {
            continued = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        await middleware.InvokeAsync(context, ambient, new StubCurrentUser(null));

        Assert.Null(ambient.CompanyId);
        Assert.True(continued);
    }

    // Lanza al leer la compañía, como el adaptador real sin un cid válido: demuestra que el middleware no la lee en anónimo.
    private sealed class StubCurrentUser(Guid? companyId) : ICurrentUser
    {
        public Guid EmployeeId => throw new InvalidOperationException();

        public Guid CompanyId => companyId ?? throw new InvalidOperationException("No se debe leer la compañía.");

        public UserRole Role => throw new InvalidOperationException();
    }
}
