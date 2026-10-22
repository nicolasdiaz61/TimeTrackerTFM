using System.Globalization;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>Topes del asistente por compañía (DD-16, DD-23): valores por defecto, rangos con extremos incluidos y validación permanente.</summary>
public sealed class AiOptionsCompanyLimitsTests
{
    private static readonly AiOptionsValidator Validator = new();

    private static AiOptions ValidAnthropic() => new() { Provider = "Anthropic", Model = "modelo-ficticio", ApiKey = "clave-prueba-123456" };

    private static void Set(AiOptions options, string key, int value) => typeof(AiOptions).GetProperty(key)!.SetValue(options, value);

    [Fact]
    public void TheDefaults_AreThirtyFiveHundredAndAHundred_WithAnHourOfIdleTime()
    {
        var options = new AiOptions();

        Assert.Equal(30, options.RequestsPerMinutePerCompany);
        Assert.Equal(500, options.DailyRequestsPerCompany);
        Assert.Equal(100, options.MaxLoadedCompanies);
        Assert.Equal(60, options.RuntimeIdleMinutes);
        Assert.True(Validator.Validate(null, ValidAnthropic()).Succeeded);
    }

    [Fact]
    public void ThePerUserLimits_AreKept()
    {
        var options = new AiOptions();

        Assert.Equal(5, options.RequestsPerMinute);
        Assert.Equal(50, options.DailyRequestsPerUser);
    }

    [Theory]
    [InlineData(nameof(AiOptions.RequestsPerMinutePerCompany), 0, 1, 600)]
    [InlineData(nameof(AiOptions.RequestsPerMinutePerCompany), 601, 1, 600)]
    [InlineData(nameof(AiOptions.DailyRequestsPerCompany), 0, 1, 100000)]
    [InlineData(nameof(AiOptions.DailyRequestsPerCompany), 100001, 1, 100000)]
    [InlineData(nameof(AiOptions.MaxLoadedCompanies), 0, 1, 1000)]
    [InlineData(nameof(AiOptions.MaxLoadedCompanies), 1001, 1, 1000)]
    [InlineData(nameof(AiOptions.RuntimeIdleMinutes), 0, 1, 1440)]
    [InlineData(nameof(AiOptions.RuntimeIdleMinutes), 1441, 1, 1440)]
    public void AValueOutOfRange_FailsNamingTheKeyAndOnlyTheBounds_EvenWithoutAProvider(string key, int value, int min, int max)
    {
        var options = new AiOptions { Provider = null };
        Set(options, key, value);

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Equal($"Ai:{key}: debe estar entre {min} y {max}.", failure);
    }

    [Theory]
    [InlineData(nameof(AiOptions.RequestsPerMinutePerCompany), 1)]
    [InlineData(nameof(AiOptions.RequestsPerMinutePerCompany), 600)]
    [InlineData(nameof(AiOptions.DailyRequestsPerCompany), 1)]
    [InlineData(nameof(AiOptions.DailyRequestsPerCompany), 100000)]
    [InlineData(nameof(AiOptions.MaxLoadedCompanies), 1)]
    [InlineData(nameof(AiOptions.MaxLoadedCompanies), 1000)]
    [InlineData(nameof(AiOptions.RuntimeIdleMinutes), 1)]
    [InlineData(nameof(AiOptions.RuntimeIdleMinutes), 1440)]
    public void TheEdgesOfTheRange_AreValid_WithAndWithoutAProvider(string key, int value)
    {
        var withoutProvider = new AiOptions();
        var withProvider = ValidAnthropic();
        Set(withoutProvider, key, value);
        Set(withProvider, key, value);

        Assert.True(Validator.Validate(null, withoutProvider).Succeeded);
        Assert.True(Validator.Validate(null, withProvider).Succeeded);
    }

    [Fact]
    public void AnUnknownProvider_StillReportsTheCompanyLimits()
    {
        var options = new AiOptions { Provider = "GitHubModels", DailyRequestsPerCompany = 0 };

        var result = Validator.Validate(null, options);

        Assert.Contains(result.Failures!, failure => failure.StartsWith("Ai:Provider", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.StartsWith("Ai:DailyRequestsPerCompany", StringComparison.Ordinal));
    }
}
