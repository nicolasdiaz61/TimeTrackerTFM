using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Api.IntegrationTests.Authentication;
using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Security;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Companies;

using static TimeTracker.Api.IntegrationTests.Support.RegistrationApiFactory;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>
/// Límite por IP del registro (company-registration, «Límite de tasa por IP del registro»): 5 por hora, ventana fija, cuentan todos
/// los intentos y el sexto no evalúa el código. Cada prueba crea su fábrica: los contadores viven en el host.
/// </summary>
[Collection("Postgres")]
public class RegistrationRateLimitTests(PostgresFixture postgres)
{
    private const string WebOrigin = "http://localhost:5172";

    private static Task<HttpResponseMessage> WrongCodeAsync(HttpClient client, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RegisterPath) { Content = JsonContent.Create(Body(inviteCode: "codigo-incorrecto-0123456789")) };
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task FiveAttemptsWithAWrongCode_AreAll403_AndTheSixthIs429WithoutEvaluatingTheCode()
    {
        var codes = new CountingInviteCodeValidator(new InviteCodeValidator([Code]));
        using var factory = CreateFactory(codes);
        var client = factory.CreateAnonymousClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await WrongCodeAsync(client)).StatusCode);
        }

        Assert.Equal(5, codes.Calls);
        var sixth = await WrongCodeAsync(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        using var body = await AccessErrorsTests.AssertProblemAsync(sixth, 429, "Demasiadas solicitudes");
        Assert.Equal("Ha realizado demasiados intentos de registro. Inténtelo de nuevo más tarde.", body.RootElement.GetProperty("detail").GetString());
        Assert.True(sixth.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.True(int.Parse(Assert.Single(retryAfter), CultureInfo.InvariantCulture) > 0);
        Assert.Equal(5, codes.Calls);
    }

    [Fact]
    public async Task SuccessfulAttemptsAlsoCount()
    {
        using var factory = CreateFactory();
        var client = factory.CreateAnonymousClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, Body())).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync(client, Body())).StatusCode);
    }

    [Fact]
    public async Task TheLimitedResponse_CarriesCorsAndTheSecurityHeaders()
    {
        using var baseFactory = new RegistrationApiFactory(postgres.ConnectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", WebOrigin));
        var client = factory.CreateAnonymousClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            _ = await WrongCodeAsync(client);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, RegisterPath) { Content = JsonContent.Create(Body()) };
        request.Headers.Add("Origin", WebOrigin);
        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal([WebOrigin], response.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(["DENY"], response.Headers.GetValues("X-Frame-Options"));
        Assert.Equal(["no-referrer"], response.Headers.GetValues("Referrer-Policy"));
        Assert.Contains("default-src 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithForwardedHeadersEnabled_AnotherIpIsNotLimited_AndTheSameIpIs()
    {
        using var factory = CreateFactory(forwardedHops: 1);
        var client = factory.CreateAnonymousClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await WrongCodeAsync(client, "203.0.113.7")).StatusCode);
        }

        var otherIp = await WrongCodeAsync(client, "203.0.113.8");
        var sameIp = await WrongCodeAsync(client, "203.0.113.7");

        Assert.Equal(HttpStatusCode.Forbidden, otherIp.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, sameIp.StatusCode);
    }

    [Fact]
    public async Task WithOneHop_AnExtraXForwardedForAddedByTheClient_FallsInTheSamePartition()
    {
        using var factory = CreateFactory(forwardedHops: 1);
        var client = factory.CreateAnonymousClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await WrongCodeAsync(client, "203.0.113.7")).StatusCode);
        }

        // La IP falsa que antepone el cliente queda a la izquierda de la que añade el proxy y no cambia la partición.
        var spoofed = await WrongCodeAsync(client, "198.51.100.9, 203.0.113.7");

        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
    }

    [Fact]
    public async Task WithoutForwardedHeaders_EveryClientSharesOnePartition()
    {
        using var factory = CreateFactory();
        var client = factory.CreateAnonymousClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            _ = await WrongCodeAsync(client, $"203.0.113.{attempt + 1}");
        }

        // Control del caso opuesto de la prueba anterior: sin ForwardedHeaders la cabecera no se obedece.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await WrongCodeAsync(client, "198.51.100.200")).StatusCode);
    }

    [Fact]
    public async Task TheRegistrationLimitAndTheLoginLimit_AreIndependent()
    {
        using var factory = CreateFactory();
        var client = factory.CreateAnonymousClient();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            _ = await WrongCodeAsync(client);
        }

        var login = await LoginEndpointTests.PostLoginAsync(client, "   ", "x");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        using var other = CreateFactory();
        var otherClient = other.CreateAnonymousClient();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginEndpointTests.PostLoginAsync(otherClient, "   ", "x")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginEndpointTests.PostLoginAsync(otherClient, "   ", "x")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await WrongCodeAsync(otherClient)).StatusCode);
    }

    [Fact]
    public async Task WithForwardedHeaders_RotatingIPv6AddressesInsideOne64_DoesNotGiveNewQuota()
    {
        using var factory = CreateFactory(forwardedHops: 1);
        var client = factory.CreateAnonymousClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await WrongCodeAsync(client, $"2001:db8:aaaa:bbbb::{attempt + 1}")).StatusCode);
        }

        var rotated = await WrongCodeAsync(client, "2001:db8:aaaa:bbbb:1234:5678:9abc:def0");
        var otherPrefix = await WrongCodeAsync(client, "2001:db8:aaaa:bbbc::1");

        Assert.Equal(HttpStatusCode.TooManyRequests, rotated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherPrefix.StatusCode);
    }

    [Fact]
    public async Task TheGlobalCap_CountsOnlyCreatedAccounts_AndRejectsTheNextOneWithoutEvaluatingTheCode()
    {
        var codes = new CountingInviteCodeValidator(new InviteCodeValidator([Code]));
        using var factory = CreateFactory(codes, forwardedHops: 1, globalLimit: 2);
        var client = factory.CreateAnonymousClient();

        // Los intentos con código incorrecto no gastan el tope global.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await WrongCodeAsync(client, $"203.0.113.{attempt + 1}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Created, (await PostWithAddressAsync(client, "198.51.100.1")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostWithAddressAsync(client, "198.51.100.2")).StatusCode);
        var callsBefore = codes.Calls;
        var capped = await PostWithAddressAsync(client, "198.51.100.3");

        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);
        Assert.True(capped.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.True(int.Parse(Assert.Single(retryAfter), CultureInfo.InvariantCulture) > 0);
        Assert.Equal(callsBefore, codes.Calls);
        using var body = await AccessErrorsTests.AssertProblemAsync(capped, 429, "Demasiadas solicitudes");
        Assert.Equal("Ha realizado demasiados intentos de registro. Inténtelo de nuevo más tarde.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TheGlobalCap_ReleasesTheSlotWhenTheRegistrationIsNotCreated()
    {
        using var factory = CreateFactory(forwardedHops: 1, globalLimit: 2);
        var otherClient = factory.CreateAnonymousClient();
        var body = Body();
        Assert.Equal(HttpStatusCode.Created, (await PostWithAddressAsync(otherClient, "198.51.100.1", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostWithAddressAsync(otherClient, "198.51.100.2", body)).StatusCode);
        // El 409 no consumió cupo: queda uno para un alta nueva.
        Assert.Equal(HttpStatusCode.Created, (await PostWithAddressAsync(otherClient, "198.51.100.3")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostWithAddressAsync(otherClient, "198.51.100.4")).StatusCode);
    }

    private static Task<HttpResponseMessage> PostWithAddressAsync(HttpClient client, string forwardedFor, Dictionary<string, object?>? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RegisterPath) { Content = JsonContent.Create(body ?? Body()) };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request, CancellationToken.None);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFactory(CountingInviteCodeValidator? codes = null, int? forwardedHops = null, int? globalLimit = null)
    {
        var settings = new Dictionary<string, string?>();
        if (forwardedHops is not null)
        {
            settings["ForwardedHeaders:Enabled"] = "true";
            settings["ForwardedHeaders:Hops"] = forwardedHops.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (globalLimit is not null)
        {
            settings["Registration:GlobalHourlyLimit"] = globalLimit.Value.ToString(CultureInfo.InvariantCulture);
        }

        var factory = new RegistrationApiFactory(postgres.ConnectionString, Code, settings);
        return codes is null
            ? factory
            : factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IInviteCodeValidator>(codes))));
    }
}
