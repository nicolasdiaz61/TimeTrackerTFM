using TimeTracker.Application.Assistant.Configuration;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class AssistantSettingsInputTests
{
    [Fact]
    public void TheInputText_NeverContainsTheKeyButKeepsTheOtherValues()
    {
        var text = Input(provider: "OpenAI", model: "m-1", credential: SecretKey).ToString();

        Assert.DoesNotContain(SecretKey, text);
        Assert.Contains("m-1", text);
    }

    [Fact]
    public void TheSaveCommandText_NeverContainsTheKey()
        => Assert.DoesNotContain(SecretKey, new SaveAssistantSettingsCommand(Input(credential: SecretKey), "0").ToString());
}
