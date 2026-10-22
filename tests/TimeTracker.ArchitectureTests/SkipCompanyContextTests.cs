using System.Reflection;
using System.Text.RegularExpressions;

using static TimeTracker.ArchitectureTests.SourceText;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// Registro con un token vigente (DD-7, DD-19, tarea 5.6b): solo <c>register-company</c> lleva el marcador que impide que el
/// middleware fije la compañía del token, y ningún camino modifica <c>uses_platform_assistant</c> tras crear la compañía.
/// </summary>
public partial class SkipCompanyContextTests
{
    private const string RegistrationEndpointsFile = "src/TimeTracker.Api/Authentication/RegistrationEndpoints.cs";

    [Fact]
    public void TheSkipCompanyContextMarker_IsAppliedToExactlyOneEndpoint_TheRegistration()
    {
        var applications = SourceFiles()
            .SelectMany(file => ApplicationPattern().Matches(StripComments(File.ReadAllText(file))).Select(_ => Relative(file)))
            .ToList();

        Assert.Equal([RegistrationEndpointsFile], applications);
    }

    [Theory]
    [InlineData("group.MapPost(\"login\", Login).SkipCompanyContext();", 1)]
    [InlineData("endpoints.MapPost(Route, Register)\n    .SkipCompanyContext()\n    .AllowAnonymous();", 1)]
    [InlineData("// .SkipCompanyContext() comentado", 0)]
    [InlineData("public static TBuilder SkipCompanyContext<TBuilder>(this TBuilder builder)", 0)]
    [InlineData("endpoints.MapGet(\"x\", X).AllowAnonymous();", 0)]
    public void ApplicationScanner_CountsOnlyCallsOfTheExtension(string source, int expected)
        => Assert.Equal(expected, ApplicationPattern().Matches(StripComments(source)).Count);

    [Fact]
    public void ThePlatformAssistantFlag_CannotBeChangedAfterTheCompanyIsCreated()
    {
        var company = Assembly.Load("TimeTracker.Domain").GetType("TimeTracker.Domain.Companies.Company", throwOnError: true)!;

        var property = company.GetProperty("UsesPlatformAssistant")!;

        Assert.NotNull(property.GetMethod);
        Assert.Null(property.SetMethod);
    }

    [Fact]
    public void TheContract_NeverMentionsThePlatformAssistantFlag()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs", "api", "openapi.json"));

        Assert.DoesNotContain("usesPlatformAssistant", text, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SourceFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal));
    }

    private static string Relative(string file) => Path.GetRelativePath(RepositoryRoot.Path, file).Replace('\\', '/');


    // Una llamada: «.SkipCompanyContext(» sin tipo genérico delante ni declaración (la declaración lleva «<TBuilder>»).
    [GeneratedRegex(@"\.SkipCompanyContext\s*\(")]
    private static partial Regex ApplicationPattern();
}
