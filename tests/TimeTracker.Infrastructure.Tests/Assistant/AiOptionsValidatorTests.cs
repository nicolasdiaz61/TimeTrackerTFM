using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AiOptionsValidatorTests
{
    private const string SecretKey = "clave-secreta-prueba";

    private static readonly AiOptionsValidator Validator = new();

    private static AiOptions ValidAnthropic() => new() { Provider = "Anthropic", Model = "modelo-ficticio", ApiKey = SecretKey };

    private static AiOptions ValidOllama() => new() { Provider = "Ollama", Model = "modelo-ficticio", Endpoint = "http://localhost:11434/" };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("None")]
    public void Validate_WithoutProvider_IsValidEvenWithInvalidLimits(string? provider)
    {
        var options = new AiOptions { Provider = provider, MaxOutputTokens = -5, TimeoutSeconds = 999 };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.False(options.IsEnabled);
    }

    [Fact]
    public void Validate_WithDefaults_HasTheDocumentedLimits()
    {
        var options = ValidAnthropic();

        Assert.Equal(800, options.MaxOutputTokens);
        Assert.Equal(5, options.MaxToolIterations);
        Assert.Equal(25, options.TimeoutSeconds);
        Assert.Equal(5, options.RequestsPerMinute);
        Assert.Equal(50, options.DailyRequestsPerUser);
        Assert.True(Validator.Validate(null, options).Succeeded);
        Assert.True(options.IsEnabled);
    }

    [Fact]
    public void Validate_OllamaWithEndpoint_IsValid()
    {
        Assert.True(Validator.Validate(null, ValidOllama()).Succeeded);
    }

    [Fact]
    public void Validate_UnknownProvider_FailsWithTheKeyName()
    {
        var options = ValidAnthropic();
        options.Provider = "GitHubModels";

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.StartsWith("Ai:Provider"));
    }

    // El valor numérico del enum (Ai__Provider=1) no es un nombre de proveedor: solo se aceptan nombres.
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("-1")]
    [InlineData(" 1 ")]
    [InlineData("Anthropic,Ollama")]
    public void Validate_NumericOrCompositeProvider_FailsWithTheKeyName(string provider)
    {
        var options = ValidAnthropic();
        options.Provider = provider;

        var result = Validator.Validate(null, options);

        Assert.Null(options.ResolvedProvider);
        Assert.False(options.IsEnabled);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.StartsWith("Ai:Provider"));
    }

    [Theory]
    [InlineData("anthropic", AiProvider.Anthropic)]
    [InlineData("OLLAMA", AiProvider.Ollama)]
    [InlineData("  Ollama  ", AiProvider.Ollama)]
    [InlineData("openai", AiProvider.OpenAI)]
    [InlineData("GEMINI", AiProvider.Gemini)]
    [InlineData("groq", AiProvider.Groq)]
    [InlineData("none", AiProvider.None)]
    [InlineData("", AiProvider.None)]
    [InlineData(null, AiProvider.None)]
    public void ResolvedProvider_AcceptsOnlyNamesIgnoringCase(string? provider, AiProvider expected)
    {
        Assert.Equal(expected, new AiOptions { Provider = provider }.ResolvedProvider);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_AnthropicWithoutApiKey_FailsWithTheKeyName(string? apiKey)
    {
        var options = ValidAnthropic();
        options.ApiKey = apiKey;

        var result = Validator.Validate(null, options);

        Assert.Contains(result.Failures!, failure => failure.StartsWith("Ai:ApiKey"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-es-una-url")]
    [InlineData("ftp://localhost/")]
    public void Validate_OllamaWithoutValidEndpoint_FailsWithTheKeyName(string? endpoint)
    {
        var options = ValidOllama();
        options.Endpoint = endpoint;

        var result = Validator.Validate(null, options);

        Assert.Contains(result.Failures!, failure => failure.StartsWith("Ai:Endpoint"));
    }

    [Fact]
    public void Validate_OllamaDoesNotRequireApiKeyAndAnthropicDoesNotRequireEndpoint()
    {
        Assert.True(Validator.Validate(null, ValidOllama()).Succeeded);
        Assert.True(Validator.Validate(null, ValidAnthropic()).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_EnabledWithoutModel_FailsWithTheKeyName(string? model)
    {
        var options = ValidAnthropic();
        options.Model = model;

        var result = Validator.Validate(null, options);

        Assert.Contains(result.Failures!, failure => failure.StartsWith("Ai:Model"));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validate_ModelLength_IsLimitedTo100(int length, bool valid)
    {
        var options = ValidAnthropic();
        options.Model = new string('m', length);

        Assert.Equal(valid, Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(nameof(AiOptions.MaxOutputTokens), 63, false)]
    [InlineData(nameof(AiOptions.MaxOutputTokens), 64, true)]
    [InlineData(nameof(AiOptions.MaxOutputTokens), 4096, true)]
    [InlineData(nameof(AiOptions.MaxOutputTokens), 4097, false)]
    [InlineData(nameof(AiOptions.MaxToolIterations), AiOptionsValidator.MinToolIterations - 1, false)]
    [InlineData(nameof(AiOptions.MaxToolIterations), AiOptionsValidator.MinToolIterations, true)]
    [InlineData(nameof(AiOptions.MaxToolIterations), AiOptionsValidator.MaxToolIterations, true)]
    [InlineData(nameof(AiOptions.MaxToolIterations), AiOptionsValidator.MaxToolIterations + 1, false)]
    [InlineData(nameof(AiOptions.TimeoutSeconds), 4, false)]
    [InlineData(nameof(AiOptions.TimeoutSeconds), 5, true)]
    [InlineData(nameof(AiOptions.TimeoutSeconds), 55, true)]
    [InlineData(nameof(AiOptions.TimeoutSeconds), 56, false)]
    [InlineData(nameof(AiOptions.RequestsPerMinute), AiOptionsValidator.MinRequestsPerMinute - 1, false)]
    [InlineData(nameof(AiOptions.RequestsPerMinute), AiOptionsValidator.MinRequestsPerMinute, true)]
    [InlineData(nameof(AiOptions.RequestsPerMinute), AiOptionsValidator.MaxRequestsPerMinute, true)]
    [InlineData(nameof(AiOptions.RequestsPerMinute), AiOptionsValidator.MaxRequestsPerMinute + 1, false)]
    [InlineData(nameof(AiOptions.DailyRequestsPerUser), AiOptionsValidator.MinDailyRequestsPerUser - 1, false)]
    [InlineData(nameof(AiOptions.DailyRequestsPerUser), AiOptionsValidator.MinDailyRequestsPerUser, true)]
    [InlineData(nameof(AiOptions.DailyRequestsPerUser), AiOptionsValidator.MaxDailyRequestsPerUser, true)]
    [InlineData(nameof(AiOptions.DailyRequestsPerUser), AiOptionsValidator.MaxDailyRequestsPerUser + 1, false)]
    public void Validate_EachLimit_IsCheckedOnItsOwn(string property, int value, bool valid)
    {
        var options = ValidAnthropic();
        typeof(AiOptions).GetProperty(property)!.SetValue(options, value);

        var result = Validator.Validate(null, options);

        Assert.Equal(valid, result.Succeeded);
        if (!valid)
        {
            Assert.Single(result.Failures!);
            Assert.StartsWith($"Ai:{property}", result.Failures!.Single());
        }
    }

    [Fact]
    public void Validate_Failures_AreInSpanishAndNeverPrintValues()
    {
        var options = new AiOptions
        {
            Provider = "Anthropic",
            ApiKey = SecretKey,
            Model = new string('m', 101),
            Endpoint = "no-es-una-url",
            MaxOutputTokens = 1,
            TimeoutSeconds = 999,
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var text = string.Join('\n', result.Failures!);
        Assert.DoesNotContain(SecretKey, text);
        Assert.DoesNotContain("no-es-una-url", text);
        Assert.DoesNotContain("999", text);
        Assert.Contains("Ai:Model", text);
        Assert.Contains("Ai:MaxOutputTokens", text);
        Assert.Contains("Ai:TimeoutSeconds", text);
        Assert.Contains("debe", text);
    }

    [Fact]
    public void Validate_UnknownProvider_NeverPrintsTheProviderValue()
    {
        var options = new AiOptions { Provider = "GitHubModels-secreto" };

        var text = string.Join('\n', Validator.Validate(null, options).Failures!);

        Assert.DoesNotContain("GitHubModels-secreto", text);
    }
    [Theory]
    [InlineData("OpenAI")]
    [InlineData("gemini")]
    [InlineData("groq")]
    public void Validate_CloudProviders_AreValidWithKeyAndModel(string provider)
    {
        var options = ValidAnthropic();
        options.Provider = provider;

        Assert.True(Validator.Validate(null, options).Succeeded);
        Assert.True(options.IsEnabled);
    }

    [Theory]
    [InlineData("OpenAI", "Ai:ApiKey")]
    [InlineData("Gemini", "Ai:ApiKey")]
    [InlineData("OpenAI", "Ai:Model")]
    [InlineData("Gemini", "Ai:Model")]
    [InlineData("Groq", "Ai:ApiKey")]
    [InlineData("Groq", "Ai:Model")]
    public void Validate_OpenAIOrGeminiWithoutKeyOrModel_FailsWithTheKeyName(string provider, string missing)
    {
        var options = ValidAnthropic();
        options.Provider = provider;
        if (missing == "Ai:ApiKey")
        {
            options.ApiKey = null;
        }
        else
        {
            options.Model = null;
        }

        Assert.Contains(Validator.Validate(null, options).Failures!, failure => failure.StartsWith(missing));
    }

    [Fact]
    public void Validate_GitHubModelsProvider_FailsWithTheKeyNameAndNeverPrintsTheValue()
    {
        var options = ValidAnthropic();
        options.Provider = "GitHubModels";

        var text = string.Join('\n', Validator.Validate(null, options).Failures!);

        Assert.Contains("Ai:Provider", text);
        Assert.DoesNotContain("GitHubModels", text);
        Assert.DoesNotContain(SecretKey, text);
    }

    [Theory]
    [InlineData("low", true)]
    [InlineData("Medium", true)]
    [InlineData(" HIGH ", true)]
    [InlineData(null, true)]
    [InlineData("extreme", false)]
    [InlineData("1", false)]
    public void Validate_Effort_AcceptsOnlyTheThreeLevelsByName(string? effort, bool valid)
    {
        var options = ValidAnthropic();
        options.Effort = effort;

        var result = Validator.Validate(null, options);

        Assert.Equal(valid, result.Succeeded);
        if (!valid)
        {
            Assert.StartsWith("Ai:Effort", result.Failures!.Single());
            Assert.DoesNotContain(effort!, result.Failures!.Single());
        }
    }

    [Theory]
    [InlineData("Anthropic,GitHubModels")]
    [InlineData("None")]
    [InlineData("Anthropic,,OpenAI,desconocido-secreto")]
    public void Validate_InvalidAllowedProviders_FailsWithTheKeyNameWithOrWithoutProvider(string list)
    {
        var withProvider = ValidAnthropic();
        withProvider.AllowedProviders = list;
        var withoutProvider = new AiOptions { AllowedProviders = list };

        foreach (var options in new[] { withProvider, withoutProvider })
        {
            var text = string.Join('\n', Validator.Validate(null, options).Failures!);

            Assert.Contains("Ai:AllowedProviders", text);
            Assert.DoesNotContain("GitHubModels", text);
            Assert.DoesNotContain("desconocido-secreto", text);
        }
    }

    [Fact]
    public void Validate_ValidAllowedProviders_Succeeds()
    {
        var options = ValidAnthropic();
        options.AllowedProviders = " anthropic, OpenAI ,Gemini,Groq,Ollama,openai";

        Assert.True(Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void Validate_SettingsPollSeconds_IsBetween1And300(int seconds, bool valid)
    {
        var options = ValidAnthropic();
        options.SettingsPollSeconds = seconds;

        var result = Validator.Validate(null, options);

        Assert.Equal(valid, result.Succeeded);
        Assert.All(result.Failures ?? [], failure => Assert.StartsWith("Ai:SettingsPollSeconds", failure));
    }

    [Fact]
    public void NewKeys_HaveDefaultsAndTheMasterKeysAreNeverValidated()
    {
        var options = ValidAnthropic();
        options.MasterKeys["1"] = "esto-no-es-base64-secreto";
        options.ActiveMasterKeyVersion = "no-es-un-numero";

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.Equal(30, options.SettingsPollSeconds);
        Assert.Null(options.Effort);
        Assert.Null(options.AllowedProviders);
    }

}
