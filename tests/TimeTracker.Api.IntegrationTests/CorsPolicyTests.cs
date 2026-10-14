using System.Net;

using Microsoft.AspNetCore.Hosting;

namespace TimeTracker.Api.IntegrationTests;

public class CorsPolicyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string WebOrigin = "http://localhost:5172";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OnlyConfiguredOrigin_ReceivesAllowOriginHeader()
    {
        var allowed = await SendWithOriginAsync(ApiFactory.ValidOrigin);
        var other = await SendWithOriginAsync("https://otro.example.test");

        Assert.True(allowed.Headers.TryGetValues(AllowOriginHeader, out var values));
        Assert.Equal([ApiFactory.ValidOrigin], values);
        Assert.False(other.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task Preflight_WithAuthorizationHeader_IsAllowedWithoutCredentials()
    {
        using var web = CreateFactoryForWebOrigin();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/time-entries");
        request.Headers.Add("Origin", WebOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        var response = await web.CreateAnonymousClient().SendAsync(request, CancellationToken.None);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal([WebOrigin], response.Headers.GetValues(AllowOriginHeader));
        Assert.Contains("authorization", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Unauthorized_HasCorsOrigin()
    {
        using var web = CreateFactoryForWebOrigin();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/time-entries");
        request.Headers.Add("Origin", WebOrigin);

        var response = await web.CreateAnonymousClient().SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal([WebOrigin], response.Headers.GetValues(AllowOriginHeader));
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFactoryForWebOrigin()
        => new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", WebOrigin));

    private Task<HttpResponseMessage> SendWithOriginAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", origin);
        return _client.SendAsync(request);
    }
}
