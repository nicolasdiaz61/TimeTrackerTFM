using TimeTracker.Application.Assistant.Configuration;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class AllowedProvidersTests
{
    [Fact]
    public void WithAList_ReturnsItsElementsIgnoringCaseSpacesAndDuplicates()
    {
        var result = AllowedProviders.Resolve(Env(allowedProviders: " anthropic , OPENAI,Anthropic "));

        Assert.Null(result.Error);
        Assert.Equal([AssistantProvider.Anthropic, AssistantProvider.OpenAI], result.Providers);
    }

    [Fact]
    public void WithoutAList_OnlyTheEnvironmentProviderIsAllowed()
    {
        var result = AllowedProviders.Resolve(Env(provider: AssistantProvider.Gemini));

        Assert.Equal([AssistantProvider.Gemini], result.Providers);
    }

    [Fact]
    public void WithoutAListAndWithANoneEnvironment_NothingIsAllowed()
        => Assert.Empty(AllowedProviders.Resolve(Env(provider: AssistantProvider.None)).Providers);

    [Fact]
    public void OllamaWithoutAnEnvironmentEndpoint_IsExcluded()
    {
        var withoutEndpoint = AllowedProviders.Resolve(Env(allowedProviders: "Anthropic,Ollama", hasEndpoint: false));
        var withEndpoint = AllowedProviders.Resolve(Env(allowedProviders: "Anthropic,Ollama", hasEndpoint: true));

        Assert.Equal([AssistantProvider.Anthropic], withoutEndpoint.Providers);
        Assert.Equal([AssistantProvider.Anthropic, AssistantProvider.Ollama], withEndpoint.Providers);
    }

    [Theory]
    [InlineData("Anthropic,GitHubModels")]
    [InlineData("None")]
    [InlineData("Anthropic,none")]
    [InlineData("1")]
    public void AnUnknownName_ProducesAnErrorNamingTheKeyWithoutPrintingTheValue(string list)
    {
        var result = AllowedProviders.Resolve(Env(allowedProviders: list));

        Assert.Empty(result.Providers);
        Assert.Contains("Ai:AllowedProviders", result.Error);
        Assert.DoesNotContain(list.Split(',')[^1], result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" anthropic , OPENAI,Anthropic ")]
    public void Validate_AcceptsAnEmptyOrWellFormedList(string? list)
        => Assert.Null(AllowedProviders.Validate(list));

    [Theory]
    [InlineData("Anthropic,GitHubModels")]
    [InlineData("None")]
    public void Validate_RejectsAnUnknownNameNamingTheKeyWithoutPrintingTheValue(string list)
    {
        var error = AllowedProviders.Validate(list);

        Assert.Contains("Ai:AllowedProviders", error);
        Assert.DoesNotContain(list.Split(',')[^1], error);
    }
}
