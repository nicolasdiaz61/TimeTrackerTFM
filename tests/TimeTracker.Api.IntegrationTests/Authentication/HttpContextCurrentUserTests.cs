using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using TimeTracker.Api.Authentication;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Authentication;

public class HttpContextCurrentUserTests
{
    [Fact]
    public void EmployeeId_ReturnsTheSubClaim()
    {
        var id = Guid.NewGuid();

        var currentUser = CreateFor(Authenticated(("sub", id.ToString())));

        Assert.Equal(id, currentUser.EmployeeId);
    }

    [Fact]
    public void EmployeeId_WithAnonymousIdentity_Throws()
    {
        var anonymous = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        Assert.Throws<InvalidOperationException>(() => CreateFor(anonymous).EmployeeId);
    }

    [Fact]
    public void EmployeeId_WithoutHttpContext_Throws()
        => Assert.Throws<InvalidOperationException>(() => new HttpContextCurrentUser(new HttpContextAccessor()).EmployeeId);

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void EmployeeId_WithInvalidSub_Throws(string sub)
        => Assert.Throws<InvalidOperationException>(() => CreateFor(Authenticated(("sub", sub))).EmployeeId);

    [Fact]
    public void EmployeeId_WithoutSubClaim_Throws()
        => Assert.Throws<InvalidOperationException>(() => CreateFor(Authenticated(("name", "Luis"))).EmployeeId);

    [Theory]
    [InlineData("Manager", UserRole.Manager)]
    [InlineData("Employee", UserRole.Employee)]
    public void Role_ReturnsTheRoleClaim(string claim, UserRole expected)
        => Assert.Equal(expected, CreateFor(Authenticated(("sub", Guid.NewGuid().ToString()), ("role", claim))).Role);

    [Fact]
    public void Role_WithAnonymousIdentity_Throws()
    {
        var anonymous = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        Assert.Throws<InvalidOperationException>(() => CreateFor(anonymous).Role);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("manager")]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Employee, Manager")]
    [InlineData("")]
    public void Role_WithUnknownRole_Throws(string claim)
        => Assert.Throws<InvalidOperationException>(() => CreateFor(Authenticated(("sub", Guid.NewGuid().ToString()), ("role", claim))).Role);

    [Fact]
    public void Role_WithoutRoleClaim_Throws()
        => Assert.Throws<InvalidOperationException>(() => CreateFor(Authenticated(("sub", Guid.NewGuid().ToString()))).Role);

    [Fact]
    public void Role_IgnoresRequestHeadersQueryAndBody()
    {
        var context = Authenticated(("sub", Guid.NewGuid().ToString()), ("role", "Employee"));
        context.Request.Headers["X-Role"] = "Manager";
        context.Request.Headers["role"] = "Manager";
        context.Request.QueryString = new QueryString("?role=Manager");
        context.Request.Body = new MemoryStream("{\"role\":\"Manager\"}"u8.ToArray());

        Assert.Equal(UserRole.Employee, CreateFor(context).Role);
    }

    private static HttpContextCurrentUser CreateFor(HttpContext context)
        => new(new HttpContextAccessor { HttpContext = context });

    private static DefaultHttpContext Authenticated(params (string Type, string Value)[] claims)
        => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                claims.Select(claim => new Claim(claim.Type, claim.Value)), authenticationType: "test")),
        };
}
