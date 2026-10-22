using System.Reflection;
using System.Text.RegularExpressions;

using static TimeTracker.ArchitectureTests.SourceText;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// Autorregistro (DD-19, tareas 5.3 y 5.4): el comparador del código de invitación no usa comparaciones que cortocircuitan,
/// Application no fija la compañía y el caso de uso no depende de ningún puerto de correo.
/// </summary>
public partial class RegistrationApplicationTests
{
    private const string ValidatorFile = "src/TimeTracker.Application/Companies/InviteCodeValidator.cs";

    [Fact]
    public void InviteCodeComparator_UsesFixedTimeEqualsAndNoShortCircuitingComparison()
    {
        var code = StripComments(File.ReadAllText(Path.Combine(RepositoryRoot.Path, ValidatorFile)));

        Assert.Matches(@"CryptographicOperations\.FixedTimeEquals\(", code);
        Assert.Empty(ForbiddenComparisons(code));
    }

    [Theory]
    [InlineData("if (candidate == code) { }", true)]
    [InlineData("if (candidate != code) { }", true)]
    [InlineData("return candidate.Equals(code);", true)]
    [InlineData("return string.Equals(candidate, code);", true)]
    [InlineData("return string.Compare(candidate, code) == 0;", true)]
    [InlineData("return String.CompareOrdinal(candidate, code) == 0;", true)]
    [InlineData("return candidate.SequenceEqual(code);", true)]
    [InlineData("return candidate.AsSpan().SequenceEqual(code);", true)]
    [InlineData("return candidate.StartsWith(code, StringComparison.Ordinal);", true)]
    [InlineData("matched |= equals(candidateHash, codeHash);", false)]
    [InlineData("return CryptographicOperations.FixedTimeEquals(left, right);", false)]
    [InlineData("var hash = SHA256.HashData(bytes);", false)]
    public void ComparisonScanner_FlagsEveryShortCircuitingComparison(string source, bool expected)
        => Assert.Equal(expected, ForbiddenComparisons(source).Count > 0);

    [Fact]
    public void Application_DoesNotKnowHowTheCompanyOfAWriteIsSet()
    {
        var offenders = ApplicationSources()
            .Where(file => ScopeSetterPattern().IsMatch(StripComments(File.ReadAllText(file))))
            .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file).Replace('\\', '/'))
            .ToList();

        Assert.True(offenders.Count == 0, $"Application no debe fijar la compañía (ICompanyContextSetter, ICompanyScopeFactory, CreateScope): {string.Join(", ", offenders)}");
        Assert.NotEmpty(ApplicationSources());
    }

    [Theory]
    [InlineData("private readonly ICompanyContextSetter _setter;", true)]
    [InlineData("var scope = factory.CreateScope(id);", true)]
    [InlineData("ICompanyScopeFactory scopes", true)]
    [InlineData("ICompanyContext context", false)]
    [InlineData("ICurrentUser user", false)]
    public void ScopeScanner_DetectsTheInfrastructureCollaborators(string source, bool expected)
        => Assert.Equal(expected, ScopeSetterPattern().IsMatch(source));

    [Fact]
    public void RegisterCompanyUseCase_DependsOnlyOnTheExpectedPorts_AndOnNoMailPort()
    {
        var application = Assembly.Load("TimeTracker.Application");
        var useCase = application.GetType("TimeTracker.Application.Companies.RegisterCompanyUseCase", throwOnError: true)!;

        var parameters = Assert.Single(useCase.GetConstructors()).GetParameters().Select(parameter => parameter.ParameterType.Name).ToList();

        Assert.Equal(
            ["IInviteCodeValidator", "IPasswordHasher", "ICompanyRegistrationStore", "SessionIssuer", "TimeProvider"],
            parameters);
        var mailTypes = application.GetTypes().Where(type => type.IsInterface && MailPattern().IsMatch(type.Name)).Select(type => type.Name).ToList();
        Assert.True(mailTypes.Count == 0, $"Application no debe declarar puertos de correo: {string.Join(", ", mailTypes)}");
        Assert.DoesNotContain(application.GetReferencedAssemblies(), reference => reference.Name!.Contains("Mail", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("IEmailSender", true)]
    [InlineData("IMailService", true)]
    [InlineData("SmtpGateway", true)]
    [InlineData("INotificationSender", true)]
    [InlineData("IPasswordHasher", false)]
    [InlineData("RegisterCompanyUseCase", false)]
    public void MailScanner_DetectsMailPortNames(string typeName, bool expected)
        => Assert.Equal(expected, MailPattern().IsMatch(typeName));

    private static List<string> ForbiddenComparisons(string code)
        => [.. ShortCircuitingComparisonPattern().Matches(code).Select(match => match.Value)];

    private static IEnumerable<string> ApplicationSources()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal));
    }


    [GeneratedRegex(@"==|!=|\.Equals\s*\(|\bEquals\s*\(|\.Compare\w*\s*\(|\bSequenceEqual\s*\(|\.StartsWith\s*\(|\.EndsWith\s*\(")]
    private static partial Regex ShortCircuitingComparisonPattern();

    [GeneratedRegex(@"\bICompanyContextSetter\b|\bICompanyScopeFactory\b|\bCreateScope\s*\(")]
    private static partial Regex ScopeSetterPattern();

    [GeneratedRegex(@"Mail|Smtp|Notification", RegexOptions.IgnoreCase)]
    private static partial Regex MailPattern();
}
