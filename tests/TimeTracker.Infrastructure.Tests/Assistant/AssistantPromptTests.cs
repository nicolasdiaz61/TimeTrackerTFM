using System.Text.RegularExpressions;

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

    // Domingo 2026-10-18: la semana en curso empieza el lunes 2026-10-12.
    [Fact]
    public void Build_IncludesTheReferenceDatesAlreadyComputedForASunday()
    {
        var prompt = AssistantPrompt.Build(new DateOnly(2026, 10, 18), UserRole.Employee);

        Assert.Contains("hoy: 2026-10-18", prompt);
        Assert.Contains("ayer: 2026-10-17", prompt);
        Assert.Contains("esta semana: fromDate 2026-10-12, toDate 2026-10-18", prompt);
        Assert.Contains("semana pasada: fromDate 2026-10-05, toDate 2026-10-11", prompt);
        Assert.Contains("este mes: fromDate 2026-10-01, toDate 2026-10-18", prompt);
        Assert.Contains("mes pasado: fromDate 2026-09-01, toDate 2026-09-30", prompt);
    }

    [Fact]
    public void Build_OnAMondayTheCurrentWeekIsJustToday()
    {
        var prompt = AssistantPrompt.Build(new DateOnly(2026, 10, 19), UserRole.Employee);

        Assert.Contains("esta semana: fromDate 2026-10-19, toDate 2026-10-19", prompt);
        Assert.Contains("semana pasada: fromDate 2026-10-12, toDate 2026-10-18", prompt);
        Assert.Contains("ayer: 2026-10-18", prompt);
    }

    [Fact]
    public void Build_AcrossYearBoundaryUsesThePreviousYear()
    {
        var prompt = AssistantPrompt.Build(new DateOnly(2027, 1, 1), UserRole.Employee);

        Assert.Contains("ayer: 2026-12-31", prompt);
        Assert.Contains("mes pasado: fromDate 2026-12-01, toDate 2026-12-31", prompt);
        Assert.Contains("este mes: fromDate 2027-01-01, toDate 2027-01-01", prompt);
        Assert.Contains("semana pasada: fromDate 2026-12-21, toDate 2026-12-27", prompt);
    }

    [Fact]
    public void Build_EveryReferencedPeriodRespectsTheLimitAndNeverEndsInTheFuture()
    {
        var start = new DateOnly(2026, 1, 1);
        for (var day = start; day < start.AddYears(1); day = day.AddDays(1))
        {
            var prompt = AssistantPrompt.Build(day, UserRole.Employee);
            var ranges = Regex.Matches(prompt, @"fromDate (\d{4}-\d{2}-\d{2}), toDate (\d{4}-\d{2}-\d{2})");

            Assert.Equal(4, ranges.Count);
            foreach (Match range in ranges)
            {
                var from = DateOnly.ParseExact(range.Groups[1].Value, "yyyy-MM-dd");
                var to = DateOnly.ParseExact(range.Groups[2].Value, "yyyy-MM-dd");
                Assert.True(to <= day, $"{day}: {range.Value}");
                Assert.True(from <= to, $"{day}: {range.Value}");
                Assert.True(to.DayNumber - from.DayNumber + 1 <= 31, $"{day}: {range.Value}");
            }
        }
    }

    [Fact]
    public void Build_TellsTheModelToUseTheDatesLiterallyAndNotToInventOrCompute()
    {
        var prompt = AssistantPrompt.Build(new DateOnly(2026, 10, 18), UserRole.Employee);

        Assert.Contains("literalmente", prompt);
        Assert.Contains("No inventes ni calcules fechas", prompt);
        Assert.Contains("workedText", prompt);
    }

    [Fact]
    public void Build_ManagerIsToldToListTheTeamFirstAndNeverPassTheName()
    {
        var manager = AssistantPrompt.Build(new DateOnly(2026, 10, 18), UserRole.Manager);
        var employee = AssistantPrompt.Build(new DateOnly(2026, 10, 18), UserRole.Employee);

        Assert.Contains("get_team_members", manager);
        Assert.Contains("nunca el nombre", manager);
        Assert.DoesNotContain("get_team_members", employee);
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
