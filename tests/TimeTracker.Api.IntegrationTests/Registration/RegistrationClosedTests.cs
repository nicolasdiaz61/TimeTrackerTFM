using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Api.IntegrationTests.Security;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Companies;

using static TimeTracker.Api.IntegrationTests.Support.RegistrationApiFactory;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>
/// Registro cerrado por defecto (company-registration, «Registro cerrado por defecto»): la ruta no existe, no se evalúa ningún código y
/// no se consulta la base de datos (la fábrica apunta a un servidor inexistente: cualquier consulta sería un 500).
/// </summary>
public class RegistrationClosedTests
{
    private const string FictitiousConnection = ApiFactory.ValidConnectionString;

    private static object FullBody() => Body();

    private static ApiFactory ClosedFactory(CountingInviteCodeValidator codes, params (string Key, string Value)[] settings)
        => new ClosedApiFactory(codes, settings);

    [Fact]
    public async Task WithoutAToken_TheRouteAnswers401LikeAnyNonExistentRoute_AndNoCodeIsEvaluated()
    {
        var codes = new CountingInviteCodeValidator();
        using var factory = ClosedFactory(codes);

        var existing = await factory.CreateAnonymousClient().PostAsJsonAsync(RegisterPath, FullBody(), CancellationToken.None);
        var missing = await factory.CreateAnonymousClient().PostAsJsonAsync("/api/auth/ruta-que-no-existe", FullBody(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, existing.StatusCode);
        Assert.Equal(missing.StatusCode, existing.StatusCode);
        Assert.Equal(0, codes.Calls);
    }

    [Fact]
    public async Task WithAValidToken_TheRouteAnswers404ProblemJson_WithoutEvaluatingACodeOrReadingTheDatabase()
    {
        var codes = new CountingInviteCodeValidator();
        using var factory = ClosedFactory(codes);

        var response = await factory.CreateClient().PostAsJsonAsync(RegisterPath, FullBody(), CancellationToken.None);
        var missing = await factory.CreateClient().PostAsJsonAsync("/api/auth/ruta-que-no-existe", FullBody(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = await AccessErrorsTests.AssertProblemAsync(response, 404, "Recurso no encontrado");
        Assert.Equal(missing.StatusCode, response.StatusCode);
        Assert.Equal(0, codes.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public void TheRouteDoesNotExistInTheEndpointTable_EvenWithCodesConfigured(string? enabled)
    {
        (string Key, string Value)[] settings = enabled is null
            ? [("Registration:InviteCodes", Code)]
            : [("Registration:Enabled", enabled), ("Registration:InviteCodes", Code)];
        using var factory = ClosedFactory(new CountingInviteCodeValidator(), settings);
        _ = factory.CreateClient();

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText);

        Assert.DoesNotContain(RegisterPath, routes);
    }

    [Fact]
    public async Task InProductionWithoutTheFlag_TheRouteDoesNotExist()
    {
        var codes = new CountingInviteCodeValidator();
        using var factory = ClosedFactory(codes).WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var withoutToken = await factory.CreateAnonymousClient().PostAsJsonAsync(RegisterPath, FullBody(), CancellationToken.None);
        var withToken = await factory.CreateClient().PostAsJsonAsync(RegisterPath, FullBody(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withToken.StatusCode);
        Assert.Equal(0, codes.Calls);
    }

    [Fact]
    public void WhenOpen_TheRouteExists_AndOnlyAcceptsPost()
    {
        using var factory = new RegistrationApiFactory(FictitiousConnection);
        _ = factory.CreateClient();

        var endpoint = Assert.Single(
            factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>(),
            candidate => candidate.RoutePattern.RawText == RegisterPath);

        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
        Assert.NotNull(endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>());
    }

    private sealed class ClosedApiFactory(CountingInviteCodeValidator codes, (string Key, string Value)[] settings) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IInviteCodeValidator>(codes)));
        }
    }
}
