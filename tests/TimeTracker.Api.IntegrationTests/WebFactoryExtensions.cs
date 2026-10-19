using Microsoft.AspNetCore.Mvc.Testing;

namespace TimeTracker.Api.IntegrationTests;

internal static class WebFactoryExtensions
{
    /// <summary>
    /// Cliente sin cabecera <c>Authorization</c>. Es una extensión de la fábrica base porque <c>WithWebHostBuilder</c>
    /// devuelve <see cref="WebApplicationFactory{TEntryPoint}"/>, no <see cref="ApiFactory"/>.
    /// </summary>
    public static HttpClient CreateAnonymousClient(this WebApplicationFactory<Program> factory)
    {
        // CreateDefaultClient también invoca ConfigureClient: hay que quitar la cabecera que añade.
        var client = factory.CreateDefaultClient();
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    private const int MaxStartAttempts = 10;

    /// <summary>
    /// Arranca la fábrica y devuelve la excepción de arranque (o <c>null</c> si arrancó). La carrera de
    /// <c>WebApplicationFactory</c> con minimal hosting (el hilo de la aplicación destruye el proveedor de servicios antes
    /// de que el hilo de la prueba reciba el error real y este ve un <see cref="ObjectDisposedException"/>) se resuelve en
    /// <see cref="ApiFactory"/> recuperando el error real (<see cref="DeferredHostFailure"/>). Si aun así llegara el
    /// <see cref="ObjectDisposedException"/> (otra fábrica, o el framework cambió), se reintenta con la misma fábrica un
    /// número acotado de veces; si todos los intentos acaban así se devuelve un <see cref="AggregateException"/> con todas
    /// las excepciones vistas, para que el fallo sea diagnosticable. Cualquier otra excepción, o la ausencia de ella, se
    /// devuelve tal cual en el primer intento: las aserciones no se debilitan.
    /// </summary>
    public static Exception? StartExpectingFailure(this WebApplicationFactory<Program> factory)
    {
        var raced = new List<Exception>();
        for (var attempt = 0; attempt < MaxStartAttempts; attempt++)
        {
            var exception = Record.Exception(() => factory.CreateClient());
            if (!IsHostDisposalRace(exception))
            {
                return exception;
            }

            raced.Add(exception!);
            Thread.Sleep(attempt * 10);
        }

        return new AggregateException(
            $"El arranque devolvió {MaxStartAttempts} veces seguidas ObjectDisposedException en lugar del error real.",
            raced);
    }

    private static bool IsHostDisposalRace(Exception? exception)
    {
        var tree = StartupFailure.Flatten(exception).ToList();
        return tree.Count > 0
            && tree.OfType<ObjectDisposedException>().Any(disposed => disposed.ObjectName == "IServiceProvider")
            && tree.OfType<Microsoft.Extensions.Options.OptionsValidationException>().Count() == 0;
    }
}
