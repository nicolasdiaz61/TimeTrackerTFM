using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Api.IntegrationTests.Security;

public class SecurityHeadersTests
{
    private static readonly string[] HeaderNames =
    [
        "X-Content-Type-Options",
        "X-Frame-Options",
        "Referrer-Policy",
        "Content-Security-Policy",
    ];

    [Fact]
    public async Task Health_Response_HasTheFourSecurityHeaders()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
    }

    [Fact]
    public async Task ApiRoutes_HaveNoStore_OnSuccessAndUnauthorized()
    {
        using var factory = new ApiFactory();

        var success = await factory.CreateClient().GetAsync("/api/auth/me", CancellationToken.None);
        var unauthorized = await factory.CreateAnonymousClient().GetAsync("/api/auth/me", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("no-store", Header(success, "Cache-Control"));
        Assert.Equal("no-store", Header(unauthorized, "Cache-Control"));
    }

    [Fact]
    public async Task Csp_IsRestrictive()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        var policy = Header(response, "Content-Security-Policy");
        Assert.False(string.IsNullOrWhiteSpace(policy));
        Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
        Assert.DoesNotContain('*', policy);
    }

    [Fact]
    public async Task ErrorResponses_HaveTheFourSecurityHeaders()
    {
        using var factory = new ApiFactory();
        var anonymous = factory.CreateAnonymousClient();

        var unauthorized = await anonymous.GetAsync("/api/auth/me", CancellationToken.None);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "   ", password = "x" }, CancellationToken.None);
        }

        var tooManyRequests = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "   ", password = "x" }, CancellationToken.None);
        var notFound = await factory.CreateClient().GetAsync("/ruta-que-no-existe", CancellationToken.None);

        using var failing = new ApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.Replace(ServiceDescriptor.Scoped<ITimeEntryRepository, ThrowingRepository>())));
        var serverError = await failing.CreateClient().PostAsync("/api/time-entries/clock-in", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, tooManyRequests.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, serverError.StatusCode);
        foreach (var response in new[] { unauthorized, tooManyRequests, notFound, serverError })
        {
            AssertSecurityHeaders(response);
        }
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        foreach (var name in HeaderNames)
        {
            Assert.True(response.Headers.Contains(name), $"{(int)response.StatusCode}: falta la cabecera {name}.");
        }
    }

    private static string Header(HttpResponseMessage response, string name)
        => Assert.Single(response.Headers.GetValues(name));

    private sealed class ThrowingRepository : ITimeEntryRepository
    {
        public Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");

        public Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");

        public Task AddAsync(TimeEntry entry, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");

        public Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");

        public Task ReplaceOpenAsync(TimeEntry closed, TimeEntry next, CancellationToken cancellationToken)
            => throw new InvalidOperationException("texto-sensible");
    }
}
