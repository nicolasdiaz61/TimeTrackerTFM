using System.Text.Json;
using System.Xml.Linq;

namespace TimeTracker.ArchitectureTests;

public class RepositoryConventionsTests
{
    private static readonly string[] ProjectPaths =
    [
        "src/TimeTracker.Domain/TimeTracker.Domain.csproj",
        "src/TimeTracker.Application/TimeTracker.Application.csproj",
        "src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj",
        "src/TimeTracker.Api/TimeTracker.Api.csproj",
        "src/TimeTracker.Web/TimeTracker.Web.csproj",
        "tests/TimeTracker.Domain.Tests/TimeTracker.Domain.Tests.csproj",
        "tests/TimeTracker.Application.Tests/TimeTracker.Application.Tests.csproj",
        "tests/TimeTracker.Infrastructure.Tests/TimeTracker.Infrastructure.Tests.csproj",
        "tests/TimeTracker.Web.Tests/TimeTracker.Web.Tests.csproj",
        "tests/TimeTracker.Api.IntegrationTests/TimeTracker.Api.IntegrationTests.csproj",
        "tests/TimeTracker.ArchitectureTests/TimeTracker.ArchitectureTests.csproj",
    ];

    public static TheoryData<string> Projects => [.. ProjectPaths];

    [Fact]
    public void Web_HasNoProjectReferences()
    {
        var web = Load("src/TimeTracker.Web/TimeTracker.Web.csproj");

        Assert.Empty(web.Descendants("ProjectReference"));
    }

    [Fact]
    public void Web_HasLoginFichajesAndTeamPagesWithClientsAndTokenHandler()
    {
        string[] required =
        [
            "src/TimeTracker.Web/Pages/Login.razor",
            "src/TimeTracker.Web/Pages/TimeEntries.razor",
            "src/TimeTracker.Web/Pages/Team.razor",
            "src/TimeTracker.Web/TimeEntries/TimeEntriesApiClient.cs",
            "src/TimeTracker.Web/Team/TeamApiClient.cs",
            "src/TimeTracker.Web/Auth/AuthApiClient.cs",
            "src/TimeTracker.Web/Auth/BearerTokenHandler.cs",
            "src/TimeTracker.Web/wwwroot/appsettings.json",
        ];

        var missing = required.Where(path => !File.Exists(Path.Combine(RepositoryRoot.Path, path))).ToList();
        Assert.True(missing.Count == 0, $"Faltan archivos de la Web: {string.Join(", ", missing)}");

        Assert.Contains("@page \"/login\"", ReadWeb("Pages/Login.razor"), StringComparison.Ordinal);
        var fichajes = ReadWeb("Pages/TimeEntries.razor");
        Assert.Contains("@page \"/fichajes\"", fichajes, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", fichajes, StringComparison.Ordinal);
        var team = ReadWeb("Pages/Team.razor");
        Assert.Contains("@page \"/equipo\"", team, StringComparison.Ordinal);
        Assert.Contains("Roles = \"Manager\"", team, StringComparison.Ordinal);

        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Path, required[^1])));
        var apiBaseUrl = settings.RootElement.GetProperty("ApiBaseUrl").GetString();
        Assert.True(Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https", "ApiBaseUrl debe ser una URI absoluta http/https.");
    }

    [Fact]
    public void Web_HasNoTemplateLeftoversAndIsBrandedInSpanish()
    {
        string[] removed = ["Pages/Counter.razor", "Pages/Weather.razor", "wwwroot/sample-data/weather.json"];
        Assert.DoesNotContain(removed, path => File.Exists(Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Web", path)));

        var index = ReadWeb("wwwroot/index.html");
        Assert.Contains("<html lang=\"es\">", index, StringComparison.Ordinal);
        Assert.Contains("<title>TimeTracker</title>", index, StringComparison.Ordinal);

        // Ningún enlace externo heredado de la plantilla ni texto de plantilla en inglés en el diseño.
        var layout = ReadWeb("Layout/MainLayout.razor") + ReadWeb("Layout/NavMenu.razor");
        Assert.DoesNotContain("learn.microsoft.com", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Navigation menu", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeTracker.Web</a>", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_HasNoRawHtmlLocalStorageOrTokenLogging()
    {
        string[] forbidden = ["MarkupString", "innerHTML", "localStorage", "document.cookie"];
        var logging = new System.Text.RegularExpressions.Regex(
            @"(Console\.Write\w*|\.Log(?:Trace|Debug|Information|Warning|Error|Critical)?)\s*\([^;]*token", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var offenders = WebSourceFiles()
            .Select(file => (file, text: File.ReadAllText(file)))
            .Where(item => forbidden.Any(word => item.text.Contains(word, StringComparison.Ordinal)) || logging.IsMatch(item.text))
            .Select(item => Path.GetRelativePath(RepositoryRoot.Path, item.file))
            .ToList();

        // Sin HTML sin escapar (XSS), sin localStorage ni cookies y sin registrar el token (WS-8, WS-11, WS-39).
        Assert.True(offenders.Count == 0, $"Uso prohibido en la Web: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Web_HasNoTimeEntriesApiException()
    {
        var web = Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Web");

        // La excepción se unifica en Http/ApiRequestException (DD-27).
        Assert.False(File.Exists(Path.Combine(web, "TimeEntries", "TimeEntriesApiException.cs")));
        Assert.True(File.Exists(Path.Combine(web, "Http", "ApiRequestException.cs")));
        Assert.DoesNotContain(WebSourceFiles(), file => File.ReadAllText(file).Contains("TimeEntriesApiException", StringComparison.Ordinal));
    }

    [Fact]
    public void Web_DoesNotHardcodeApiUrl()
    {
        var offenders = WebSourceFiles()
            .Where(file => File.ReadAllText(file).Contains("localhost:5274", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file))
            .ToList();

        // La URL de la API vive solo en wwwroot/appsettings.json (UI-11).
        Assert.True(offenders.Count == 0, $"URL de la API fija en el código: {string.Join(", ", offenders)}");
    }

    private static string ReadWeb(string relativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Web", relativePath));

    private static IEnumerable<string> WebSourceFiles()
    {
        var webDirectory = Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Web");
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(webDirectory, "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".razor", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void AllProjects_TargetNet10(string projectPath)
    {
        var project = Load(projectPath);

        Assert.Equal("net10.0", project.Descendants("TargetFramework").Single().Value);
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void PackageReferences_DoNotDeclareVersion(string projectPath)
    {
        var withVersion = Load(projectPath).Descendants("PackageReference")
            .Where(reference => reference.Attribute("Version") is not null)
            .Select(reference => (string?)reference.Attribute("Include"))
            .ToList();

        // Las versiones viven en Directory.Packages.props (gestión central de paquetes).
        Assert.True(withVersion.Count == 0, $"{projectPath} declara Version en: {string.Join(", ", withVersion)}");
    }

    public static TheoryData<string> InnerLayerProjects =>
    [
        "src/TimeTracker.Domain/TimeTracker.Domain.csproj",
        "src/TimeTracker.Application/TimeTracker.Application.csproj",
    ];

    [Theory]
    [MemberData(nameof(InnerLayerProjects))]
    public void Domain_And_Application_DeclareNoPackageOrFrameworkReferences(string projectPath)
    {
        var declared = Load(projectPath).Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => $"{element.Name.LocalName} {(string?)element.Attribute("Include")}")
            .ToList();

        // Las capas internas solo dependen del marco base (y Application, de Domain): sin paquetes ni marcos extra.
        Assert.True(declared.Count == 0, $"{projectPath} declara referencias no permitidas: {string.Join(", ", declared)}");
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Authentication.JwtBearer", "src/TimeTracker.Api/TimeTracker.Api.csproj")]
    [InlineData("Microsoft.AspNetCore.Components.Authorization", "src/TimeTracker.Web/TimeTracker.Web.csproj")]
    public void AuthPackages_AreDeclaredOnlyInTheirProject(string package, string owner)
    {
        var central = Load("Directory.Packages.props").Descendants("PackageVersion")
            .SingleOrDefault(reference => (string?)reference.Attribute("Include") == package);
        Assert.True(central is not null, $"Falta PackageVersion de {package} en Directory.Packages.props.");
        Assert.Equal("10.0.10", (string?)central.Attribute("Version"));

        var referencing = ProjectPaths
            .Where(path => Load(path).Descendants("PackageReference")
                .Any(reference => (string?)reference.Attribute("Include") == package))
            .ToList();
        Assert.Equal([owner], referencing);
    }

    [Fact]
    public void AiPackages_AreDeclaredOnlyInInfrastructureWithCentralVersion()
    {
        static bool IsAiPackage(string? name)
            => name is not null
                && (name.StartsWith("Microsoft.Extensions.AI", StringComparison.Ordinal)
                    || name.Equals("Anthropic", StringComparison.Ordinal)
                    || name.Equals("OllamaSharp", StringComparison.Ordinal));

        var central = Load("Directory.Packages.props").Descendants("PackageVersion")
            .Where(reference => IsAiPackage((string?)reference.Attribute("Include")))
            .ToList();
        Assert.Contains(central, reference => (string?)reference.Attribute("Include") == "Microsoft.Extensions.AI"
            && (string?)reference.Attribute("Version") == "10.10.0");

        // Solo Infrastructure declara paquetes de IA (los proyectos de pruebas pueden usar Abstractions); sin Version propia.
        var offenders = ProjectPaths
            .Where(path => path.StartsWith("src/", StringComparison.Ordinal) && path != "src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj")
            .Where(path => Load(path).Descendants("PackageReference").Any(reference => IsAiPackage((string?)reference.Attribute("Include"))))
            .ToList();
        Assert.True(offenders.Count == 0, $"Declaran paquetes de IA fuera de Infrastructure: {string.Join(", ", offenders)}");

        var infrastructure = Load("src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj").Descendants("PackageReference")
            .Where(reference => IsAiPackage((string?)reference.Attribute("Include")))
            .ToList();
        Assert.Contains(infrastructure, reference => (string?)reference.Attribute("Include") == "Microsoft.Extensions.AI");
        Assert.All(infrastructure, reference => Assert.Null(reference.Attribute("Version")));
    }

    [Fact]
    public void Src_DoesNotHardcodeModelNames()
    {
        // El modelo y el proveedor se eligen solo por configuración (AGENTS §7): ningún nombre de modelo en el código.
        var modelName = new System.Text.RegularExpressions.Regex(
            @"claude-|\bgpt-|qwen|gemini-|llama[0-9]|mistral|phi[0-9]|haiku|sonnet|\bopus\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var separators = new[] { $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}" };

        var offenders = Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".razor", StringComparison.Ordinal) || file.EndsWith(".json", StringComparison.Ordinal))
            .Where(file => !separators.Any(file.Contains))
            .Where(file => modelName.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file))
            .ToList();

        Assert.True(offenders.Count == 0, $"Nombres de modelo en el código: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Solution_DoesNotDependOnAspNetCoreIdentity()
    {
        var offenders = ProjectPaths.Append("Directory.Packages.props")
            .Where(path => Load(path).Descendants()
                .Any(element => ((string?)element.Attribute("Include"))?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) == true))
            .ToList();

        // La identidad es propia (JWT + PasswordHasher del marco compartido): sin ASP.NET Core Identity (DD-1).
        Assert.True(offenders.Count == 0, $"Dependen de ASP.NET Core Identity: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void DotnetTools_DeclaresDotnetEf_10_0_12()
    {
        var manifest = Path.Combine(RepositoryRoot.Path, ".config/dotnet-tools.json");

        Assert.True(File.Exists(manifest), "Falta .config/dotnet-tools.json (herramienta local dotnet-ef).");
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var version = document.RootElement.GetProperty("tools").GetProperty("dotnet-ef").GetProperty("version").GetString();
        Assert.Equal("10.0.12", version);
    }

    [Fact]
    public void AppSettings_ContainNoSensitiveKeys()
    {
        string[] forbidden = ["ConnectionStrings", "Database", "Identity", "Jwt", "Seed"];
        var apiDirectory = Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Api");

        var found = Directory.GetFiles(apiDirectory, "appsettings*.json")
            .SelectMany(file => FindForbiddenKeys(JsonDocument.Parse(File.ReadAllText(file)).RootElement, forbidden)
                .Select(name => $"{Path.GetFileName(file)}: {name}"))
            .ToList();

        // Estas claves solo llegan por variables de entorno (AGENTS.md §6): nunca versionadas.
        Assert.True(found.Count == 0, $"appsettings versiona claves prohibidas: {string.Join(", ", found)}");
    }

    [Theory]
    [InlineData(".gitignore")]
    [InlineData(".dockerignore")]
    public void EnvFile_IsIgnored(string ignoreFile)
    {
        // Solo se lee el archivo de exclusiones: el contenido de `.env` no se abre nunca.
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot.Path, ignoreFile)).Select(line => line.Trim());

        Assert.Contains(".env", lines);
    }

    [Fact]
    public void Code_HasNoProvisionalIdentityLeftovers()
    {
        string[] leftovers = ["DevelopmentCurrentUser", "ProvisionalIdentity", "Identity:DevelopmentEmployeeId"];
        // LegacyIdentityKeyTests comprueba que una clave heredada se ignora (SC-16); esta prueba contiene los literales.
        string[] exempt = ["LegacyIdentityKeyTests.cs", nameof(RepositoryConventionsTests) + ".cs"];

        var offenders = new[] { "src", "tests" }
            .SelectMany(root => Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, root), "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsBuildOutput(file) && !exempt.Contains(Path.GetFileName(file)))
            .Where(file => leftovers.Any(text => File.ReadAllText(file).Contains(text, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file))
            .ToList();

        Assert.True(offenders.Count == 0, $"Quedan restos de la identidad provisional en: {string.Join(", ", offenders)}");
    }

    [Theory]
    [InlineData("docker-compose.yml")]
    [InlineData(".env.example")]
    [InlineData("README.md")]
    [InlineData("openspec/config.yaml")]
    public void DeliveryFiles_HaveNoProvisionalIdentity(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, relativePath));

        Assert.DoesNotContain("Identity__DevelopmentEmployeeId", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Identity:DevelopmentEmployeeId", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_HasHttpContextCurrentUser_AndNoProvisionalIdentityTypes()
    {
        var api = Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Api");

        Assert.True(File.Exists(Path.Combine(api, "Authentication", "HttpContextCurrentUser.cs")), "Falta HttpContextCurrentUser.cs.");
        Assert.False(Directory.Exists(Path.Combine(api, "Identity")), "La carpeta Api/Identity (identidad provisional) debe desaparecer.");
    }

    private static bool IsBuildOutput(string file)
        => file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static IEnumerable<string> FindForbiddenKeys(JsonElement root, string[] forbidden)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (forbidden.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
            {
                yield return property.Name;
            }

            if (property.Name.Equals("Cors", StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("AllowedOrigins", out _))
            {
                yield return "Cors:AllowedOrigins";
            }
        }
    }

    private static XDocument Load(string relativePath)
        => XDocument.Load(Path.Combine(RepositoryRoot.Path, relativePath));
}
