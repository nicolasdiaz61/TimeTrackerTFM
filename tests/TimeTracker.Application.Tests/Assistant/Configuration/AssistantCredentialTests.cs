using TimeTracker.Application.Assistant.Configuration;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class AssistantCredentialTests
{
    [Fact]
    public void ToString_NeverReturnsTheValue()
    {
        var credential = new AssistantCredential(SecretKey);

        Assert.DoesNotContain(SecretKey, credential.ToString());
        Assert.Equal("[clave redactada]", credential.ToString());
    }

    [Fact]
    public void InterpolationAndContainingRecords_DoNotLeakTheValue()
    {
        var credential = new AssistantCredential(SecretKey);

        Assert.DoesNotContain(SecretKey, $"{credential}");
        Assert.DoesNotContain(SecretKey, Env(apiKey: SecretKey).ToString());
    }

    [Fact]
    public void TheValue_IsStillAvailableToThePortsThatNeedIt()
        => Assert.Equal(SecretKey, new AssistantCredential(SecretKey).Value);
}
