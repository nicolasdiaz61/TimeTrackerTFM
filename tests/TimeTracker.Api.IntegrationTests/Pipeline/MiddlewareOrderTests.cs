using System.Net;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Application.Identity;

namespace TimeTracker.Api.IntegrationTests.Pipeline;

/// <summary>
/// V-10: el contexto de compañía se fija tras la autenticación y antes de la autorización. Un manejador de autorización de prueba
/// (se ejecuta dentro de <c>UseAuthorization</c>) anota la compañía que ve el contexto en ese momento.
/// </summary>
public class MiddlewareOrderTests
{
    private static readonly Guid CompanyA = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");

    [Fact]
    public async Task WithAValidToken_TheContextAlreadyHoldsTheCompanyOfTheTokenWhenAuthorizationRuns()
    {
        var probe = new Probe();
        using var factory = new ProbedFactory(probe);
        var token = TestTokens.Create(Guid.NewGuid(), "Employee", factory.Clock.GetUtcNow(), companyId: CompanyA.ToString());

        var response = await GetAsync(factory, "/api/auth/me", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([(Guid?)CompanyA], probe.Seen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("no-es-un-guid")]
    public async Task WithAnInvalidOrMissingCid_TheContextHasNoCompanyAndNothingThrows(string? cid)
    {
        var probe = new Probe();
        using var factory = new ProbedFactory(probe);
        var token = TestTokens.Create(Guid.NewGuid(), "Employee", factory.Clock.GetUtcNow(), companyId: cid);

        var response = await GetAsync(factory, "/api/auth/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal([(Guid?)null], probe.Seen);
    }

    [Fact]
    public async Task WithoutAToken_TheContextHasNoCompany()
    {
        var probe = new Probe();
        using var factory = new ProbedFactory(probe);

        var response = await GetAsync(factory, "/api/auth/me", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal([(Guid?)null], probe.Seen);
    }

    [Fact]
    public async Task Health_DoesNotDependOnTheCompany()
    {
        var probe = new Probe();
        using var factory = new ProbedFactory(probe);

        var response = await GetAsync(factory, "/health", token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetAsync(ApiFactory factory, string path, string? token)
    {
        using var client = factory.CreateAnonymousClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.GetAsync(path, CancellationToken.None);
    }

    private sealed class Probe
    {
        public List<Guid?> Seen { get; } = [];
    }

    private sealed class ProbedFactory(Probe probe) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(probe);
                services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, ProbeHandler>());
            });
        }
    }

    // La política de respaldo exige un usuario autenticado: su requisito lo evalúa UseAuthorization, no antes.
    private sealed class ProbeHandler(ICompanyContext company, Probe probe) : AuthorizationHandler<DenyAnonymousAuthorizationRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DenyAnonymousAuthorizationRequirement requirement)
        {
            probe.Seen.Add(company.CompanyId);
            return Task.CompletedTask;
        }
    }
}
