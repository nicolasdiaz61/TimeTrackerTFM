using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;


namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// assistant-endpoint, «Contrato OpenAPI del asistente»: solo <c>post</c> con sus cinco respuestas y Bearer, sin identidad ni compañía
/// en la operación, y los endpoints anónimos exactamente tres con el registro cerrado y cuatro con él abierto. El contrato se lee del
/// documento vivo de la API (Development), que es el mismo que el build exporta a <c>docs/api/openapi.json</c>.
/// </summary>
public class AssistantContractAndAuthorizationCoverageTests
{
    private const string AssistantPath = "/api/asistente";

    private static async Task<JsonDocument> LiveContractAsync(ApiFactory factory)
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/openapi/v1.json", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TheAssistantOperation_IsOnlyPostWithItsFiveResponsesAndBearer_AndNoIdentifiers()
    {
        using var factory = new ApiFactory();
        using var contract = await LiveContractAsync(factory);

        var path = contract.RootElement.GetProperty("paths").GetProperty(AssistantPath);
        Assert.Equal(["post"], path.EnumerateObject().Select(operation => operation.Name));
        var operation = path.GetProperty("post");
        Assert.Equal(["200", "400", "401", "429", "503"], operation.GetProperty("responses").EnumerateObject().Select(response => response.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("413", operation.GetProperty("responses").EnumerateObject().Select(response => response.Name));
        Assert.False(operation.TryGetProperty("parameters", out _));
        var text = operation.GetRawText();
        foreach (var forbidden in new[] { "employeeId", "memberId", "companyId" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }

        var security = operation.TryGetProperty("security", out var own) ? own : contract.RootElement.GetProperty("security");
        Assert.Contains(security.EnumerateArray(), requirement => requirement.TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task AGetToTheAssistant_ByAnAuthenticatedUser_Returns405()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync(AssistantPath, CancellationToken.None);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public void WithTheRegistrationClosed_TheAnonymousEndpointsAreExactlyThree()
    {
        using var factory = new ApiFactory();

        Assert.Equal(["GET /health", "GET /openapi/{documentName}.json", "POST /api/auth/login"], AnonymousEndpoints(factory));
    }

    [Fact]
    public void WithTheRegistrationOpen_TheAnonymousEndpointsAreExactlyFour()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Registration:Enabled", "true");
            builder.UseSetting("Registration:InviteCodes", "codigo-prueba-0123456789");
        });

        Assert.Equal(
            ["GET /health", "GET /openapi/{documentName}.json", "POST /api/auth/login", "POST /api/auth/register-company"],
            AnonymousEndpoints(factory));
    }

    private static List<string> AnonymousEndpoints(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        _ = factory.CreateClient();
        return factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
