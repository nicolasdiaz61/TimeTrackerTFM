using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
    public void TeamEndpoints_RequireTheManagerPolicy()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();

        var team = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/team", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal(2, team.Count);
        Assert.All(team, endpoint => Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), data => data.Policy == "Manager"));
    }
}
