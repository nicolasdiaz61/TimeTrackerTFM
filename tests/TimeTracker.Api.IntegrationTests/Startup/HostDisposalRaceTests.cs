using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>
/// Reproduce de forma determinista la carrera de <c>WebApplicationFactory</c> (el hilo de la aplicación ya ha fallado y
/// destruido el proveedor de servicios antes de que el hilo de la prueba lo toque) y comprueba que la fábrica sigue
/// entregando el error de arranque real.
/// </summary>
public class HostDisposalRaceTests
{
    private const string ConnectionStringKey = "ConnectionStrings:Default";

    [Fact]
    public void Startup_WhenTheHostIsAlreadyDisposed_StillSurfacesTheRealFailure()
    {
        using var factory = new DisposedBeforeStartFactory().WithWebHostBuilder(builder => builder.UseSetting(ConnectionStringKey, "   "));

        var exception = Record.Exception(() => factory.CreateClient());

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, ConnectionStringKey);
        Assert.DoesNotContain(StartupFailure.Flatten(exception), inner => inner is ObjectDisposedException);
    }

    [Fact]
    public void StartExpectingFailure_WhenTheHostIsAlreadyDisposed_ReturnsTheRealFailureOnTheFirstAttempt()
    {
        using var factory = new DisposedBeforeStartFactory().WithWebHostBuilder(builder => builder.UseSetting(ConnectionStringKey, "   "));

        var exception = factory.StartExpectingFailure();

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, ConnectionStringKey);
    }

    /// <summary>Espera, antes de arrancar, a que el hilo de la aplicación haya fallado y destruido el host.</summary>
    private sealed class DisposedBeforeStartFactory : ApiFactory
    {
        protected override void OnHostBuilt(IHost host)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    _ = host.Services.GetService(typeof(IHostApplicationLifetime));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                Thread.Sleep(1);
            }

            throw new TimeoutException("El host no se destruyó: el arranque no falló como se esperaba.");
        }
    }
}
