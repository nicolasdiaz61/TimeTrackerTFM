using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TimeTracker.Api.IntegrationTests.Authentication;

public class MeEndpointTests
{
    private const string MePath = "/api/auth/me";

    [Fact]
    public async Task Me_WithValidToken_ReturnsClaimsData()
    {
        using var factory = new ApiFactory();
        var userId = Guid.NewGuid();
        var token = TestTokens.Create(userId, "Employee", factory.Clock.GetUtcNow(), name: "Luis Ejemplo", email: "luis.empleado@example.test");

        var response = await GetMeAsync(factory, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(userId, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("luis.empleado@example.test", body.RootElement.GetProperty("email").GetString());
        Assert.Equal("Luis Ejemplo", body.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("Employee", body.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Me_AsManager_ReturnsManagerRole()
    {
        using var factory = new ApiFactory();
        var token = TestTokens.Create(Guid.NewGuid(), "Manager", factory.Clock.GetUtcNow());

        var response = await GetMeAsync(factory, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal("Manager", body.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync(MePath, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_DoesNotQueryTheDatabase()
    {
        // La fábrica base apunta a db.invalid: si /me tocara la base, fallaría con 500.
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync(MePath, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    internal static async Task<HttpResponseMessage> GetMeAsync(ApiFactory factory, string token)
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(MePath, CancellationToken.None);
    }
}
