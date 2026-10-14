using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;

namespace TimeTracker.Api.IntegrationTests.Security;

/// <summary>
/// Tras el proxy del despliegue (Railway) la IP de la conexión es la del proxy: el limitador de login solo distingue
/// clientes si se confía en <c>X-Forwarded-For</c>. Se activa por configuración (<c>ForwardedHeaders:Enabled</c>). El
/// borde de Railway entrega <c>X-Forwarded-For: &lt;cliente&gt;, &lt;borde&gt;</c> (la dirección del borde cambia de una petición
/// a otra): con dos saltos de confianza el cliente es la penúltima entrada y lo que se antepone a la izquierda no cuenta.
/// </summary>
public class ForwardedHeadersTests
{
    private const string EnabledKey = "ForwardedHeaders:Enabled";
    private const string HopsKey = "ForwardedHeaders:Hops";

    [Fact]
    public async Task Disabled_ForwardedForIsIgnored_SoTheLimiterSeesOneClient()
    {
        using var factory = new FactoryBehindProxy(enabled: false);
        var statuses = await SendAttemptsAsync(factory.CreateAnonymousClient(), attempt => $"203.0.113.{attempt + 1}, 84.17.44.9");

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task Enabled_DistinctClientAddresses_AreLimitedSeparately()
    {
        using var factory = new FactoryBehindProxy(enabled: true);
        var statuses = await SendAttemptsAsync(factory.CreateAnonymousClient(), attempt => $"203.0.113.{attempt + 1}, 84.17.44.9");

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Enabled_SameClientBehindEdgeAddressesThatVary_IsLimited()
    {
        using var factory = new FactoryBehindProxy(enabled: true);
        var statuses = await SendAttemptsAsync(factory.CreateAnonymousClient(), attempt => $"203.0.113.7, 84.17.44.{attempt + 1}");

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task Enabled_AddressesPrependedByTheClient_CannotEvadeTheLimit()
    {
        using var factory = new FactoryBehindProxy(enabled: true);
        var statuses = await SendAttemptsAsync(factory.CreateAnonymousClient(), attempt => $"198.51.100.{attempt + 1}, 203.0.113.7, 84.17.44.9");

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task Enabled_HopsCanBeConfigured()
    {
        using var factory = new FactoryBehindProxy(enabled: true, hops: 1);
        var statuses = await SendAttemptsAsync(factory.CreateAnonymousClient(), attempt => $"203.0.113.{attempt + 1}, 84.17.44.9");

        // Con un solo salto el cliente es la última entrada (constante): se limita.
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    private static async Task<List<HttpStatusCode>> SendAttemptsAsync(HttpClient client, Func<int, string> forwardedFor)
    {
        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 11; attempt++)
        {
            // Correo en blanco: 401 sin consultar la base; lo que se prueba es la partición del limitador.
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "   ", password = "x" }),
            };
            request.Headers.Add("X-Forwarded-For", forwardedFor(attempt));
            statuses.Add((await client.SendAsync(request, CancellationToken.None)).StatusCode);
        }

        return statuses;
    }

    private sealed class FactoryBehindProxy(bool enabled, int? hops = null) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting(EnabledKey, enabled ? "true" : "false");
            if (hops is not null)
            {
                builder.UseSetting(HopsKey, hops.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
