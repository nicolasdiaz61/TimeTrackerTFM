using System.Net;

using Microsoft.AspNetCore.Hosting;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>
/// Una clave <c>Identity:DevelopmentEmployeeId</c> heredada de un entorno antiguo no tiene ningún efecto (SC-16).
/// Este archivo es la única excepción de la prueba de restos de la identidad provisional (D3).
/// </summary>
public class LegacyIdentityKeyTests
{
    private const string LegacyKey = "Identity:DevelopmentEmployeeId";

    [Fact]
    public async Task Startup_WithLegacyIdentityKey_IgnoresItAndGetTimeEntriesWithoutTokenIs401()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(
            builder => builder.UseSetting(LegacyKey, "5b1f0c1e-7a44-4f0e-9c52-3d8a6e2b9f10"));

        var response = await factory.CreateAnonymousClient().GetAsync("/api/time-entries", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
