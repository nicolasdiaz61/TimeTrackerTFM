using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using TimeTracker.Api.Authentication;

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

    private static HttpContextCurrentUser CreateFor(HttpContext context)
        => new(new HttpContextAccessor { HttpContext = context });

    private static DefaultHttpContext Authenticated(params (string Type, string Value)[] claims)
        => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                claims.Select(claim => new Claim(claim.Type, claim.Value)), authenticationType: "test")),
        };
}
