using System.Net;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// assistant-configuration, «El Manager no las edita»: los topes por compañía son del entorno. El cuerpo de
/// <c>PUT /api/asistente/configuracion</c> no los admite y el contrato no los describe.
/// </summary>
public sealed class CompanyLimitsNotEditableTests
{
    [Theory]
    [InlineData("dailyRequestsPerCompany")]
    [InlineData("requestsPerMinutePerCompany")]
    [InlineData("maxLoadedCompanies")]
    public async Task Put_WithACompanyLimitProperty_Returns400AndSavesNothing(string property)
    {
        var store = new CountingSettingsStore();
        using var factory = new AssistantConfigurationApiFactory(store: store);

        var response = await factory.CreateClient().PutJsonAsync($$"""{"provider":"Ollama","model":"modelo-x","concurrencyToken":"0","{{property}}":5}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((0, 0), (store.GetCalls, store.WriteCalls));
    }

    [Fact]
    public void TheContract_DescribesNoCompanyLimitProperty()
    {
        var contract = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "api", "openapi.json")));

        Assert.Contains("/api/asistente/configuracion", contract, StringComparison.Ordinal);
        foreach (var name in new[] { "dailyRequestsPerCompany", "requestsPerMinutePerCompany", "maxLoadedCompanies" })
        {
            Assert.DoesNotContain(name, contract, StringComparison.OrdinalIgnoreCase);
        }
    }
}
