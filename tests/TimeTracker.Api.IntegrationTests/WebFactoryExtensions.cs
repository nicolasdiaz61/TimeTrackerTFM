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
    /// Arranca la fábrica y devuelve la excepción de arranque (o <c>null</c> si arrancó). Con minimal hosting,
    /// <c>WebApplicationFactory</c> ejecuta <c>app.Run()</c> en otro hilo; cuando el arranque falla (justo lo que
    /// comprueban las pruebas de arranque) ese hilo destruye el proveedor de servicios y, según la planificación de
    /// hilos, el hilo de la prueba puede tocarlo antes de recibir el error real y ver un
    /// <see cref="ObjectDisposedException"/> del <c>IServiceProvider</c> en vez de la validación de opciones. Es una carrera del
    /// propio framework (sin estado compartido entre pruebas), que aparece con la CPU saturada. Solo ese resultado se
    /// reintenta, con la misma fábrica; cualquier otra excepción, o la ausencia de ella, se devuelve tal cual.
    /// </summary>
    public static Exception? StartExpectingFailure(this WebApplicationFactory<Program> factory)
    {
        Exception? exception = null;
        for (var attempt = 0; attempt < MaxStartAttempts; attempt++)
        {
            exception = Record.Exception(() => factory.CreateClient());
            if (!IsHostDisposalRace(exception))
            {
                return exception;
            }
        }

        return exception;
    }

    private static bool IsHostDisposalRace(Exception? exception)
    {
        var tree = StartupFailure.Flatten(exception).ToList();
        return tree.Count > 0
            && tree.OfType<ObjectDisposedException>().Any(disposed => disposed.ObjectName == "IServiceProvider")
            && tree.OfType<Microsoft.Extensions.Options.OptionsValidationException>().Count() == 0;
    }
}
