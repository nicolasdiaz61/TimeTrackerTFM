using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using TimeTracker.Api.Security;

namespace TimeTracker.Api.IntegrationTests.Security;

public class AuthorizationCoverageTests
{
    [Fact]
    public void AnonymousEndpoints_AreExactlyHealthLoginAndOpenApi()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();

        var anonymous = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Cualquier endpoint nuevo nace protegido (política de respaldo): solo estos tres pueden ser anónimos.
        Assert.Equal(["GET /health", "GET /openapi/{documentName}.json", "POST /api/auth/login"], anonymous);
    }

    [Fact]
    public void AssistantEndpoint_IsProtectedLimitedPerUserAndCapsTheBody()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();

        var endpoint = Assert.Single(
            factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>(),
            candidate => candidate.RoutePattern.RawText == "/api/asistente");

        // Sin [AllowAnonymous]: lo protege la política de respaldo (cualquier usuario autenticado).
        Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
        Assert.Equal("assistant", endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(32 * 1024, endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>()!.MaxRequestBodySize);
    }

    [Fact]
    public void TeamEndpoints_RequireTheManagerPolicy()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();

        var team = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/team", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal(2, team.Count);
        Assert.All(team, endpoint => Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), data => data.Policy == AuthorizationPolicies.Manager));
    }

    [Fact]
    public void EmployeeEndpoints_RequireTheManagerPolicy_AndEachHasItsOwnRateLimitPolicy()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();

        var employees = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/employees", StringComparison.Ordinal) == true)
            .ToDictionary(endpoint => endpoint.RoutePattern.RawText!.TrimEnd('/'), endpoint => endpoint);

        Assert.Equal(["/api/employees", "/api/employees/{memberId:guid}/temporary-password"], employees.Keys.Order(StringComparer.Ordinal));
        Assert.All(employees.Values, endpoint =>
        {
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), data => data.Policy == AuthorizationPolicies.Manager);
        });
        Assert.Equal("employee-create", employees["/api/employees"].Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal("temporary-password", employees["/api/employees/{memberId:guid}/temporary-password"].Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    public void ChangePasswordEndpoint_IsProtectedByTheFallbackPolicy_LimitedPerUser_AndAllowedWhilePending()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();

        var endpoint = Assert.Single(
            factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>(),
            candidate => candidate.RoutePattern.RawText == "/api/auth/change-password");

        Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
        Assert.Equal("change-password", endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()!.PolicyName);
        Assert.NotNull(endpoint.Metadata.GetMetadata<AllowWhilePasswordChangePendingMetadata>());
    }

    [Fact]
    public async Task ManagerPolicy_IsRegisteredOnceAndRequiresTheManagerRole()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();

        var policy = await factory.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(AuthorizationPolicies.Manager);

        Assert.Equal("Manager", AuthorizationPolicies.Manager);
        var roles = Assert.Single(policy!.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>());
        Assert.Equal(["Manager"], roles.AllowedRoles);
    }
}
