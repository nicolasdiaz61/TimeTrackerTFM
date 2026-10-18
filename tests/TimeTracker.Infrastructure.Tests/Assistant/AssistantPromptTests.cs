using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class AssistantPromptTests
{
    [Theory]
    [InlineData(2026, 10, 18, "domingo")]
    [InlineData(2026, 10, 19, "lunes")]
    [InlineData(2026, 10, 22, "jueves")]
    public void Build_IncludesTodayWithTheSpanishWeekday(int year, int month, int day, string weekday)
    {
        var prompt = AssistantPrompt.Build(new DateOnly(year, month, day), UserRole.Employee);

        Assert.Contains($"{weekday} {year:0000}-{month:00}-{day:00}", prompt);
    }

    [Fact]
    public void Build_StatesTimeZoneLanguageAndPlainText()
    {
        var prompt = AssistantPrompt.Build(new DateOnly(2026, 10, 18), UserRole.Employee);

        Assert.Contains("hora de España peninsular", prompt);
        Assert.Contains("en español", prompt);
        Assert.Contains("texto plano", prompt);
    }

    [Theory]
    [InlineData(UserRole.Manager, "Manager")]
    [InlineData(UserRole.Employee, "Employee")]
    public void Build_IncludesTheRole(UserRole role, string expected)
    {
        var prompt = AssistantPrompt.Build(new DateOnly(2026, 10, 18), role);

        Assert.Contains($"Rol del usuario: {expected}", prompt);
    }

    [Fact]
    public void Build_ManagerMentionsDirectReportsAndEmployeeDoesNot()
    {
        var manager = AssistantPrompt.Build(new DateOnly(2026, 10, 18), UserRole.Manager);
        var employee = AssistantPrompt.Build(new DateOnly(2026, 10, 18), UserRole.Employee);

        Assert.Contains("reportes directos", manager);
        Assert.DoesNotContain("reportes directos", employee);
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Employee)]
    public void Build_ContainsNoSecretsOrConfigurationKeys(UserRole role)
    {
        var prompt = AssistantPrompt.Build(new DateOnly(2026, 10, 18), role);

        Assert.DoesNotContain("Ai:ApiKey", prompt);
        Assert.DoesNotContain("Jwt:SigningKey", prompt);
        Assert.DoesNotContain("ApiKey", prompt);
        Assert.DoesNotContain("SigningKey", prompt);
    }
}
