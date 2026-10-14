using System.Net;

using Microsoft.AspNetCore.Hosting;

namespace TimeTracker.Api.IntegrationTests.Security;

public class OpenApiExposureTests
{
    [Fact]
    public async Task OpenApi_InDevelopment_IsServedWithoutToken()
    {
        // ApiFactory arranca en Development por defecto.
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/openapi/v1.json", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_InProduction_WithToken_Returns404_AndWithoutToken_Returns401()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var withToken = await factory.CreateClient().GetAsync("/openapi/v1.json", CancellationToken.None);
        var withoutToken = await factory.CreateAnonymousClient().GetAsync("/openapi/v1.json", CancellationToken.None);

        // Nunca se sirve el documento: la ruta no existe y la política de respaldo responde 401 a quien no se identifica.
        Assert.Equal(HttpStatusCode.NotFound, withToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
    }
}
