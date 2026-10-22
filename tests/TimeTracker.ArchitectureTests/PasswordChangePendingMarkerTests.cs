using System.Text.RegularExpressions;
using static TimeTracker.ArchitectureTests.SourceText;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// Cambio de contraseña obligatorio (DD-21, tarea 6.11): solo <c>GET /api/auth/me</c> y <c>POST /api/auth/change-password</c> llevan el
/// marcador que los exime del 403 del cambio pendiente; un endpoint nuevo con el marcador debe hacer fallar esta prueba.
/// </summary>
public partial class PasswordChangePendingMarkerTests
{
    private const string AuthEndpointsFile = "src/TimeTracker.Api/Authentication/AuthEndpoints.cs";

    [Fact]
    public void TheMarker_IsAppliedToExactlyTwoEndpoints_MeAndChangePassword_BothInTheAuthEndpoints()
    {
        var applications = SourceFiles()
            .SelectMany(file => ApplicationPattern().Matches(StripComments(File.ReadAllText(file))).Select(_ => Relative(file)))
            .ToList();

        Assert.Equal([AuthEndpointsFile, AuthEndpointsFile], applications);
        var authEndpoints = StripComments(File.ReadAllText(Path.Combine(RepositoryRoot.Path, AuthEndpointsFile)));
        Assert.Matches(@"MapGet\(""me""[\s\S]*?\.AllowWhilePasswordChangePending\(\)", authEndpoints);
        Assert.Matches(@"MapPost\(""change-password""[\s\S]*?\.AllowWhilePasswordChangePending\(\)", authEndpoints);
    }

    [Theory]
    [InlineData("group.MapGet(\"me\", Me).AllowWhilePasswordChangePending();", 1)]
    [InlineData("group.MapPost(\"x\", X)\n    .AllowWhilePasswordChangePending()\n    .AllowAnonymous();", 1)]
    [InlineData("// .AllowWhilePasswordChangePending() comentado", 0)]
    [InlineData("public static TBuilder AllowWhilePasswordChangePending<TBuilder>(this TBuilder builder)", 0)]
    [InlineData("endpoints.MapGet(\"x\", X).AllowAnonymous();", 0)]
    public void ApplicationScanner_CountsOnlyCallsOfTheExtension(string source, int expected)
        => Assert.Equal(expected, ApplicationPattern().Matches(StripComments(source)).Count);

    private static IEnumerable<string> SourceFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal));
    }

    private static string Relative(string file) => Path.GetRelativePath(RepositoryRoot.Path, file).Replace('\\', '/');


    // Una llamada: «.AllowWhilePasswordChangePending(» sin la declaración (que lleva «<TBuilder>» tras el nombre).
    [GeneratedRegex(@"\.AllowWhilePasswordChangePending\s*\(")]
    private static partial Regex ApplicationPattern();
}
