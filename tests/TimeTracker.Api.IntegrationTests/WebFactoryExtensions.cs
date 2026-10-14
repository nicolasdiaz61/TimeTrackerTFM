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
}
