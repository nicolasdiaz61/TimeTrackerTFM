using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace TimeTracker.Api.IntegrationTests.Security;

public class FallbackPolicyTests
{
    private const string ProbePath = "/sonda-sin-atributos";

    [Fact]
    public async Task Health_WithoutToken_Returns200()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/time-entries/clock-in")]
    [InlineData("POST", "/api/time-entries/clock-out")]
    [InlineData("GET", "/api/time-entries")]
    [InlineData("GET", "/api/team/members")]
    [InlineData("GET", "/api/team/members/5b1f0c1e-7a44-4f0e-9c52-3d8a6e2b9f10/time-entries")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string method, string path)
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), path), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnmappedEndpoint_WithoutToken_Returns401_AndWithTokenReachesIt()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IStartupFilter, ProbeEndpointStartupFilter>()));

        var anonymous = await factory.CreateAnonymousClient().GetAsync(ProbePath, CancellationToken.None);
        var authenticated = await factory.CreateClient().GetAsync(ProbePath, CancellationToken.None);

        // El control con token demuestra que la sonda existe: el 401 se debe a la política de respaldo, no a una ruta ausente.
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task NonexistentRouteAndPostHealth_WithoutToken_Return401()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateAnonymousClient();

        var missing = await client.GetAsync("/ruta-que-no-existe", CancellationToken.None);
        var post = await client.PostAsync("/health", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
    }

    [Fact]
    public async Task NonexistentRouteAndPostHealth_WithToken_Return404And405()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var missing = await client.GetAsync("/ruta-que-no-existe", CancellationToken.None);
        var post = await client.PostAsync("/health", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    // Endpoint sin ningún atributo de autorización. El enrutado externo (el que la plantilla de WebApplication respeta
    // cuando ya existe) lo selecciona y la canalización real de Program.cs (autenticación, autorización) lo atraviesa.
    private sealed class ProbeEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapGet(ProbePath, () => "ok"));
        };
    }
}
