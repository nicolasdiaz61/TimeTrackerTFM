using TimeTracker.Application.Assistant.Configuration;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class AssistantSettingsRulesTests
{
    private static SettingsValidation Validate(AssistantSettingsInput input, AssistantEnvironmentDefaults? env = null, AssistantSettings? stored = null)
        => AssistantSettingsRules.Validate(input, env ?? Env(), stored);

    private static string[] Fields(SettingsValidation validation) => [.. validation.Errors.Select(e => e.Field)];

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(55, true)]
    [InlineData(56, false)]
    public void TimeoutSeconds_IsAcceptedOnlyBetween5And55(int value, bool valid)
    {
        var result = Validate(Input(timeoutSeconds: value));

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(["timeoutSeconds"], Fields(result));
        }
    }

    [Theory]
    [InlineData(63, false)]
    [InlineData(64, true)]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public void MaxOutputTokens_IsAcceptedOnlyBetween64And4096(int value, bool valid)
    {
        var result = Validate(Input(maxOutputTokens: value));

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(["maxOutputTokens"], Fields(result));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>")]
    [InlineData("ñ")]
    public void Model_WithInvalidContent_IsRejectedNamingTheFieldWithoutTheValue(string model)
    {
        var error = Assert.Single(Validate(Input(model: model)).Errors);

        Assert.Equal("model", error.Field);
        Assert.Contains("model", error.Message);
        Assert.DoesNotContain(model.Trim() + "\"", error.Message);
    }

    [Fact]
    public void Model_With101Characters_IsRejected()
        => Assert.Equal(["model"], Fields(Validate(Input(model: new string('a', 101)))));

    [Theory]
    [InlineData("a1.b_c-d:e/f@g")]
    [InlineData("x")]
    public void Model_WithAllowedCharacters_IsAccepted(string model)
        => Assert.True(Validate(Input(model: model)).IsValid);

    [Fact]
    public void Model_Omitted_FallsBackToTheEnvironmentModel()
        => Assert.True(Validate(Input(model: null)).IsValid);

    [Fact]
    public void Model_OmittedAndWithoutEnvironmentModel_IsRejected()
        => Assert.Equal(["model"], Fields(Validate(Input(model: null), Env(model: null))));

    [Theory]
    [InlineData("low")]
    [InlineData("Medium")]
    [InlineData("HIGH")]
    public void Effort_WithAKnownLevel_IsAcceptedWithoutCaseSensitivity(string effort)
    {
        var result = Validate(Input(effort: effort));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Value!.Effort);
    }

    [Fact]
    public void Effort_Extreme_IsRejected()
        => Assert.Equal(["effort"], Fields(Validate(Input(effort: "extreme"))));

    [Fact]
    public void Effort_WithAProviderThatDoesNotSupportIt_IsRejected()
        => Assert.Equal(["effort"], Fields(Validate(Input(provider: "None", effort: "high"))));

    [Theory]
    [InlineData("GitHubModels")]
    [InlineData("3")]
    [InlineData("")]
    public void Provider_OutsideTheCatalog_IsRejected(string provider)
        => Assert.Equal(["provider"], Fields(Validate(Input(provider: provider))));

    [Fact]
    public void Provider_NotInTheAllowedList_IsRejected()
    {
        var result = Validate(Input(provider: "Gemini", credential: SecretKey), Env(allowedProviders: "Anthropic"));

        Assert.Equal(["provider"], Fields(result));
    }

    [Fact]
    public void Provider_InTheAllowedList_IsAccepted()
        => Assert.True(Validate(Input(provider: "openai", credential: SecretKey), Env(allowedProviders: "Anthropic,OpenAI")).IsValid);

    [Fact]
    public void Provider_None_IsAlwaysValidEvenWhenNotListed()
    {
        var result = Validate(Input(provider: "None", model: null, timeoutSeconds: null, maxOutputTokens: null), Env(allowedProviders: "Anthropic"));

        Assert.True(result.IsValid);
        Assert.Equal(AssistantProvider.None, result.Value!.Provider);
    }

    [Fact]
    public void Credential_WithProviderNone_IsRejectedNamingTheFieldWithoutTheValue()
    {
        var error = Assert.Single(Validate(Input(provider: "None", model: null, timeoutSeconds: null, maxOutputTokens: null, credential: SecretKey)).Errors);

        Assert.Equal("credential", error.Field);
        Assert.Contains("credential", error.Message);
        Assert.DoesNotContain(SecretKey, error.Message);
    }

    [Fact]
    public void BlankCredential_WithProviderNone_IsTreatedAsOmitted()
        => Assert.True(Validate(Input(provider: "None", model: null, timeoutSeconds: null, maxOutputTokens: null, credential: "  ")).IsValid);

    [Fact]
    public void Credential_WhenTheProviderNeedsOneAndNoneExists_IsRejected()
        => Assert.Equal(["credential"], Fields(Validate(Input(provider: "Anthropic"), Env(apiKey: null))));

    [Fact]
    public void Credential_FromTheEnvironmentOnlyCountsForTheEnvironmentProvider()
        => Assert.Equal(["credential"], Fields(Validate(Input(provider: "Gemini"), Env(provider: AssistantProvider.Anthropic, allowedProviders: "Anthropic,Gemini"))));

    [Fact]
    public void Credential_StoredForTheSameProvider_IsEnough()
    {
        var stored = Stored(AssistantProvider.Gemini, credential: new EncryptedCredential([1], new byte[12], 1));

        Assert.True(Validate(Input(provider: "Gemini"), Env(apiKey: null, allowedProviders: "Gemini"), stored).IsValid);
    }

    [Fact]
    public void Credential_NewOne_IsEnough()
        => Assert.True(Validate(Input(provider: "Gemini", credential: SecretKey), Env(apiKey: null, allowedProviders: "Gemini")).IsValid);

    [Fact]
    public void Ollama_NeedsNoKeyButNeedsTheEnvironmentEndpoint()
    {
        Assert.True(Validate(Input(provider: "Ollama"), Env(apiKey: null, hasEndpoint: true, allowedProviders: "Ollama")).IsValid);
        Assert.Equal(["provider"], Fields(Validate(Input(provider: "Ollama"), Env(apiKey: null, hasEndpoint: false, allowedProviders: "Ollama"))));
    }

    [Fact]
    public void Errors_AreInSpanishAndNeverIncludeValues()
    {
        var result = Validate(Input(provider: "Anthropic", model: "<script>", timeoutSeconds: 4, maxOutputTokens: 63, effort: "extreme", credential: SecretKey));

        Assert.Equal(["model", "timeoutSeconds", "maxOutputTokens", "effort"], Fields(result));
        Assert.All(result.Errors, e =>
        {
            Assert.DoesNotContain(SecretKey, e.Message);
            Assert.DoesNotContain("<script>", e.Message);
            Assert.DoesNotContain("extreme", e.Message);
        });
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("0", true)]
    [InlineData("1234", true)]
    public void ConcurrencyToken_IsRequiredAndNumeric(string? token, bool valid)
        => Assert.Equal(valid, AssistantSettingsRules.ValidateToken(token) is null);
}
