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
    public void Web_HasTheWorkSchedulesPageForManagersOnlyWithItsMenuLink()
    {
        var page = ReadWeb("Pages/WorkSchedulesPage.razor");

        Assert.Contains("@page \"/jornadas\"", page, StringComparison.Ordinal);
        Assert.Contains("Roles = \"Manager\"", page, StringComparison.Ordinal);
        var menu = ReadWeb("Layout/NavMenu.razor");
        var managerBlock = menu[menu.IndexOf("Roles=\"Manager\"", StringComparison.Ordinal)..];
        Assert.Contains("href=\"jornadas\"", managerBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"jornadas\"", menu[..menu.IndexOf("Roles=\"Manager\"", StringComparison.Ordinal)], StringComparison.Ordinal);
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

    [Fact]
    public void Web_DeclaresNoAiPackagesNorUsesTheirNamespaces()
    {
        var packages = Load("src/TimeTracker.Web/TimeTracker.Web.csproj").Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(name => name is not null
                && (name.StartsWith("Microsoft.Extensions.AI", StringComparison.Ordinal)
                    || name.Equals("Anthropic", StringComparison.Ordinal)
                    || name.Equals("OllamaSharp", StringComparison.Ordinal)))
            .ToList();
        Assert.True(packages.Count == 0, $"La Web declara paquetes de IA: {string.Join(", ", packages)}");

        // La Web solo llama a POST /api/asistente: ningún código suyo toca el SDK de un proveedor.
        var offenders = WebSourceFiles()
            .Where(file => File.ReadAllText(file).Split('\n').Any(line =>
                line.TrimStart().StartsWith("using ", StringComparison.Ordinal)
                && (line.Contains("Microsoft.Extensions.AI", StringComparison.Ordinal)
                    || line.Contains("Anthropic", StringComparison.Ordinal)
                    || line.Contains("OllamaSharp", StringComparison.Ordinal))))
            .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file))
            .ToList();
        Assert.True(offenders.Count == 0, $"La Web usa espacios de nombres de IA: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Web_HasTheAssistantPageForAnyRoleWithPlainTextAndNoBrowserStorage()
    {
        var page = ReadWeb("Pages/Assistant.razor");

        Assert.Contains("@page \"/asistente\"", page, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Roles", page, StringComparison.Ordinal);
        Assert.Contains("class=\"assistant-text\"", page, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", page, StringComparison.Ordinal);
        Assert.Contains("white-space: pre-wrap", ReadWeb("wwwroot/css/app.css"), StringComparison.Ordinal);

        // Conversación solo en memoria del componente: ni almacenamiento del navegador ni URL (la prueba general ya veta localStorage).
        var assistantSources = WebSourceFiles()
            .Where(file => file.Contains($"{Path.DirectorySeparatorChar}Assistant", StringComparison.Ordinal))
            .Select(file => File.ReadAllText(file));
        Assert.DoesNotContain(assistantSources.Append(page), text => text.Contains("sessionStorage", StringComparison.Ordinal)
            || text.Contains("IJSRuntime", StringComparison.Ordinal));

        var menu = ReadWeb("Layout/NavMenu.razor");
        Assert.Contains("href=\"asistente\"", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_HasTheAssistantConfigurationPageOnlyForManagersWithPlainTextAndNoBrowserStorage()
    {
        var pagePath = Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Web", "Pages", "AssistantConfiguration.razor");
        Assert.True(File.Exists(pagePath), "Falta src/TimeTracker.Web/Pages/AssistantConfiguration.razor.");
        var page = ReadWeb("Pages/AssistantConfiguration.razor");

        // El rol del JWT, igual que /equipo; la Web no referencia Domain, así que no puede usar nameof(UserRole.Manager).
        Assert.Contains("@page \"/asistente/configuracion\"", page, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize(Roles = \"Manager\")]", page, StringComparison.Ordinal);

        // Texto plano y sin almacenamiento del navegador (DD-17, V-5): ni la configuración ni la clave salen del componente.
        var sources = WebSourceFiles()
            .Where(file => Path.GetFileName(file).StartsWith("AssistantConfiguration", StringComparison.Ordinal))
            .Select(file => File.ReadAllText(file))
            .ToList();
        Assert.Contains(page, sources);
        string[] forbidden = ["MarkupString", "localStorage", "sessionStorage", "IJSRuntime", "innerHTML", "type=\"url\""];
        Assert.All(sources, text =>
        {
            Assert.DoesNotContain(forbidden, word => text.Contains(word, StringComparison.Ordinal));
            Assert.DoesNotContain("endpoint", text, StringComparison.OrdinalIgnoreCase);
        });

        // La clave: campo de contraseña, desactivado sin almacenamiento seguro y nunca escrito en otro elemento.
        Assert.Contains("type=\"password\"", page, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"new-password\"", page, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@(!form.CredentialEnabled)\"", page, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(page, @"form\.Credential\b"));

        // Avisos y acciones exigidos por la spec.
        Assert.Contains("data-testid=\"data-sharing-notice\"", page, StringComparison.Ordinal);
        Assert.Contains("tercero", page, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"company-scope-notice\"", page, StringComparison.Ordinal);
        // El alcance nombra la compañía de la sesión (DD-18): el literal «toda la empresa» ya no existe y el texto vive en una clase pura.
        Assert.Contains("AssistantConfigurationNotice.Scope(SessionClaims.CompanyName(", page, StringComparison.Ordinal);
        Assert.DoesNotContain("toda la empresa", page, StringComparison.Ordinal);
        Assert.DoesNotContain("toda la instancia", page, StringComparison.Ordinal);
        Assert.Contains("Guardar", page, StringComparison.Ordinal);
        Assert.Contains("Probar conexión", page, StringComparison.Ordinal);
        Assert.Contains("Restablecer al entorno", page, StringComparison.Ordinal);
        Assert.Contains("AssistantConfigurationNotice.ResetWarning(", page, StringComparison.Ordinal);
        Assert.Contains("aunque otro responsable", ReadWeb("Assistant/AssistantConfigurationNotice.cs"), StringComparison.Ordinal);
        Assert.Contains("clave configurada", page, StringComparison.Ordinal);

        // La página nunca muestra ni envía un identificador de compañía: el nombre sale del claim de la sesión.
        Assert.DoesNotContain("companyId", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"cid\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_LinksTheAssistantConfigurationOnlyInTheManagerBlockAndLeavesTheChatPageAlone()
    {
        var menu = ReadWeb("Layout/NavMenu.razor");
        var managerStart = menu.IndexOf("<AuthorizeView Roles=\"Manager\"", StringComparison.Ordinal);
        var managerEnd = menu.IndexOf("</AuthorizeView>", managerStart, StringComparison.Ordinal);
        Assert.True(managerStart >= 0, "Falta el bloque de menú del Manager.");
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(menu, "asistente/configuracion"));
        var managerBlock = menu[managerStart..managerEnd];
        Assert.Contains("href=\"asistente/configuracion\"", managerBlock, StringComparison.Ordinal);
        Assert.Contains("href=\"equipo\"", managerBlock, StringComparison.Ordinal);

        Assert.Contains("AssistantConfigurationApiClient", ReadWeb("Program.cs"), StringComparison.Ordinal);
        var chat = ReadWeb("Pages/Assistant.razor");
        Assert.DoesNotContain("Configuration", chat, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", chat, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_TheChatLinkMatchesOnlyItsExactRoute_SoItIsNotActiveOnTheConfigurationPage()
    {
        var menu = ReadWeb("Layout/NavMenu.razor");

        Assert.Matches("""<NavLink class="nav-link" href="asistente" Match="NavLinkMatch\.All">""", menu);
    }

    [Fact]
    public void Web_TheConfigurationPageDisablesTheResetConfirmationWhileBusyAndFlagsAFailedRereadAfterReset()
    {
        var page = ReadWeb("Pages/AssistantConfiguration.razor");

        Assert.Matches("""@onclick="ResetAsync" disabled="@\(!form\.CanConfirmReset\)">Sí, restablecer""", page);
        Assert.Contains("form.Fail(ConfigurationOperation.ReloadAfterReset, loadError)", page, StringComparison.Ordinal);
    }

    private static readonly string[] SecretStorage = ["MarkupString", "localStorage", "sessionStorage", "IJSRuntime", "innerHTML"];

    [Fact]
    public void Web_HasTheRegistrationPageAnonymousWithMaskedSecretsAndNoBrowserStorage()
    {
        var page = ReadWeb("Pages/Register.razor");

        Assert.Contains("@page \"/registro\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@attribute [Authorize", page, StringComparison.Ordinal);
        AssertNoSecretStorageOrUrl(page, ReadWeb("Registration/RegisterForm.cs"), ReadWeb("Registration/RegistrationContract.cs"));
        AssertPasswordFieldsAreMasked(page, expectedPasswordFields: 3);
        Assert.Contains("@inject IRegistrationApi", page, StringComparison.Ordinal);
        Assert.Contains("new RegisterForm(", page, StringComparison.Ordinal);
        AssertSpanishWithoutTechnicalData(page, "Crear compañía", "Contraseña", "Código de invitación");
    }

    [Fact]
    public void Web_HasTheChangePasswordPageForAnySessionWithMaskedSecretsAndNoBrowserStorage()
    {
        var page = ReadWeb("Pages/ChangePassword.razor");

        Assert.Contains("@page \"/cambiar-contrasena\"", page, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Roles", page, StringComparison.Ordinal);
        AssertNoSecretStorageOrUrl(page, ReadWeb("Account/ChangePasswordForm.cs"), ReadWeb("Account/AccountApiClient.cs"));
        AssertPasswordFieldsAreMasked(page, expectedPasswordFields: 3);
        Assert.Contains("new ChangePasswordForm(", page, StringComparison.Ordinal);
        Assert.Contains("ChangePasswordForm.MandatoryChangeNotice", page, StringComparison.Ordinal);
        AssertSpanishWithoutTechnicalData(page, "Contraseña actual", "Contraseña nueva", "Cambiar contraseña");
    }

    [Fact]
    public void Web_HasTheTeamCreationPageOnlyForManagersWithoutRemovalOrEditionControls()
    {
        var page = ReadWeb("Pages/TeamMemberCreate.razor");

        Assert.Contains("@page \"/equipo/alta\"", page, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize(Roles = \"Manager\")]", page, StringComparison.Ordinal);
        AssertNoSecretStorageOrUrl(page, ReadWeb("Team/TeamMemberCreationForm.cs"), ReadWeb("Team/TemporaryPasswordReveal.cs"), ReadWeb("Team/EmployeesApiClient.cs"));
        Assert.Contains("new TeamMemberCreationForm(", page, StringComparison.Ordinal);
        Assert.Contains("<TemporaryPasswordNotice", page, StringComparison.Ordinal);
        AssertSpanishWithoutTechnicalData(page, "Alta de personas", "Correo electrónico", "Responsable");

        // Solo alta: sin controles de baja ni de edición (WS «Solo alta»).
        string[] removalOrEdition = ["Eliminar", "Borrar", "Dar de baja", "Editar", "Modificar", "Desactivar"];
        Assert.DoesNotContain(removalOrEdition, word => page.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Web_TheTeamPageOffersToRegenerateTheTemporaryPasswordWithConfirmationAndTheSharedNotice()
    {
        var page = ReadWeb("Pages/Team.razor");
        var notice = ReadWeb("Shared/TemporaryPasswordNotice.razor");

        Assert.Contains("Regenerar contraseña temporal", page, StringComparison.Ordinal);
        Assert.Contains("new TeamMemberTemporaryPassword(", page, StringComparison.Ordinal);
        Assert.Contains("role=\"alertdialog\"", page, StringComparison.Ordinal);
        Assert.Contains("<TemporaryPasswordNotice", page, StringComparison.Ordinal);
        Assert.Contains("TemporaryPasswordReveal.Notice", notice, StringComparison.Ordinal);
        AssertNoSecretStorageOrUrl(page, notice, ReadWeb("Team/TeamMemberTemporaryPassword.cs"));
        AssertSpanishWithoutTechnicalData(page, "Regenerar contraseña temporal");
    }

    [Fact]
    public void Web_NeverReadsOrSendsACompanyIdentifier()
    {
        var offenders = WebSourceFiles()
            .Select(file => (file, text: File.ReadAllText(file)))
            .Where(item => item.text.Contains("companyId", StringComparison.OrdinalIgnoreCase)
                || item.text.Contains("\"cid\"", StringComparison.Ordinal)
                || item.text.Contains("CompanyIdClaim", StringComparison.Ordinal))
            .Select(item => Path.GetRelativePath(RepositoryRoot.Path, item.file))
            .ToList();

        // Solo se lee «cname» (nombre) y «mcp» (cambio pendiente); «cid» no se usa para ninguna decisión (WS «Sin identificador de compañía»).
        Assert.True(offenders.Count == 0, $"La Web usa un identificador de compañía: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Web_HasNoPublicLinkToTheRegistrationPage_AndTheMenuHidesEverythingButTheChangeAndSignOutWhilePending()
    {
        var offenders = WebSourceFiles()
            .Where(file => Path.GetFileName(file) != "Register.razor")
            .Select(file => (file, text: File.ReadAllText(file)))
            .Where(item => item.text.Contains("href=\"registro\"", StringComparison.Ordinal)
                || item.text.Contains("href=\"/registro\"", StringComparison.Ordinal)
                || item.text.Contains("NavigateTo(\"registro\"", StringComparison.Ordinal)
                || item.text.Contains("NavigateTo(\"/registro\"", StringComparison.Ordinal))
            .Select(item => Path.GetRelativePath(RepositoryRoot.Path, item.file))
            .ToList();
        Assert.True(offenders.Count == 0, $"Enlace a /registro: {string.Join(", ", offenders)}");

        var menu = ReadWeb("Layout/NavMenu.razor");
        Assert.DoesNotContain("registro", menu, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SessionClaims.IsPasswordChangePending(", menu, StringComparison.Ordinal);
        Assert.Contains("SessionClaims.CompanyName(", menu, StringComparison.Ordinal);
        Assert.Contains("href=\"cambiar-contrasena\"", menu, StringComparison.Ordinal);
        Assert.Contains("Cambiar contraseña", menu, StringComparison.Ordinal);
        Assert.Contains("Cerrar sesión", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_TheMenuLinksTheTeamCreationOnlyInTheManagerBlock()
    {
        var menu = ReadWeb("Layout/NavMenu.razor");
        var managerStart = menu.IndexOf("<AuthorizeView Roles=\"Manager\"", StringComparison.Ordinal);
        var managerEnd = menu.IndexOf("</AuthorizeView>", managerStart, StringComparison.Ordinal);

        Assert.True(managerStart >= 0, "Falta el bloque de menú del Manager.");
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(menu, "equipo/alta"));
        Assert.Contains("href=\"equipo/alta\"", menu[managerStart..managerEnd], StringComparison.Ordinal);
        Assert.Contains("Alta de personas", menu[managerStart..managerEnd], StringComparison.Ordinal);
    }

    [Fact]
    public void Web_TheServicesAreRegisteredAndEveryRouteIsGuardedByThePasswordChangeGate()
    {
        var program = ReadWeb("Program.cs");
        string[] registrations = ["EmployeesApiClient", "AccountApiClient", "IRegistrationApi", "ILoginApi", "ISessionSignIn", "IPasswordChangeApi", "IEmployeesApi"];
        Assert.All(registrations, name => Assert.Contains(name, program, StringComparison.Ordinal));

        // Registro y login anónimos (sin BearerTokenHandler); cambio de contraseña y alta con él.
        Assert.Contains("new AuthApiClient(new HttpClient { BaseAddress = apiBaseUri })", program, StringComparison.Ordinal);
        Assert.Contains("new AccountApiClient(CreateAuthorizedClient(", program, StringComparison.Ordinal);
        Assert.Contains("new EmployeesApiClient(CreateAuthorizedClient(", program, StringComparison.Ordinal);

        var app = ReadWeb("App.razor");
        Assert.Contains("<PasswordChangeGate>", app, StringComparison.Ordinal);
        Assert.Contains("PasswordChangeRedirect.TargetFor(", ReadWeb("Shared/PasswordChangeGate.razor"), StringComparison.Ordinal);
    }

    private static void AssertNoSecretStorageOrUrl(params string[] sources)
    {
        Assert.All(sources, text =>
        {
            Assert.DoesNotContain(SecretStorage, word => text.Contains(word, StringComparison.Ordinal));

            // La contraseña, el código y la contraseña temporal nunca viajan en la URL ni en una navegación.
            Assert.DoesNotMatch(@"NavigateTo\([^)]*(Password|InviteCode|Temporary|Reveal)", text);
            Assert.DoesNotContain("QueryString", text, StringComparison.Ordinal);
        });
    }

    private static void AssertPasswordFieldsAreMasked(string page, int expectedPasswordFields)
    {
        Assert.Equal(expectedPasswordFields, System.Text.RegularExpressions.Regex.Matches(page, "type=\"password\"").Count);
        Assert.Equal(expectedPasswordFields, System.Text.RegularExpressions.Regex.Matches(page, "autocomplete=\"(new|current)-password\"|autocomplete=\"off\"").Count);
    }

    private static void AssertSpanishWithoutTechnicalData(string page, params string[] expectedLabels)
    {
        Assert.All(expectedLabels, label => Assert.Contains(label, page, StringComparison.Ordinal));

        // Solo el marcado visible: el bloque @code puede nombrar excepciones al capturarlas sin mostrarlas.
        var codeStart = page.IndexOf("@code", StringComparison.Ordinal);
        var markup = codeStart < 0 ? page : page[..codeStart];
        string[] technical = ["Exception", "StackTrace", "Bearer", "access_token", "AccessToken", "InviteCodes", "companyId", "\"cid\""];
        Assert.DoesNotContain(technical, word => markup.Contains(word, StringComparison.OrdinalIgnoreCase));
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
    public void OpenAIAdapter_IsDeclaredOnlyInInfrastructureAndItsNamespaceIsUsedOnlyThere()
    {
        var central = Load("Directory.Packages.props").Descendants("PackageVersion")
            .Single(reference => (string?)reference.Attribute("Include") == "Microsoft.Extensions.AI.OpenAI");
        Assert.Equal("10.10.0", (string?)central.Attribute("Version"));

        // El SDK `OpenAI` llega transitivo: ningún proyecto lo declara directamente, y solo Infrastructure usa su espacio de nombres.
        var declared = ProjectPaths
            .Where(path => Load(path).Descendants("PackageReference")
                .Any(reference => (string?)reference.Attribute("Include") is "OpenAI" or "Microsoft.Extensions.AI.OpenAI"))
            .ToList();
        Assert.Equal(["src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj"], declared.Where(path => path.StartsWith("src/", StringComparison.Ordinal)));

        var offenders = SourceFiles("src")
            .Where(file => !file.Contains("TimeTracker.Infrastructure", StringComparison.Ordinal))
            .Where(file => File.ReadAllLines(file).Any(line => line.TrimStart().StartsWith("using ", StringComparison.Ordinal)
                && (line.Contains("OpenAI", StringComparison.Ordinal))))
            .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file))
            .ToList();
        Assert.True(offenders.Count == 0, $"Usan el espacio de nombres de OpenAI fuera de Infrastructure: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void InfrastructureAssistant_BuildsNoUriOutsideTheApprovedSyntacticForms()
    {
        // Sin URL elegida por el Manager (SSRF, DD-3): las únicas direcciones salen de una constante fija o del entorno (Ollama). Guarda sintáctica; sus límites están en UriCreation.
        var offenders = SourceFiles("src/TimeTracker.Infrastructure/Assistant")
            .Where(file => CreatesUnapprovedUri(Path.GetFileName(file), File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file))
            .ToList();
        Assert.True(offenders.Count == 0, $"Crean un Uri fuera de ProviderEndpoints y del endpoint de Ollama: {string.Join(", ", offenders)}");

        Assert.Contains("new Uri(", File.ReadAllText(Path.Combine(RepositoryRoot.Path, "src/TimeTracker.Infrastructure/Assistant/ProviderEndpoints.cs")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("var u = new Uri(\"https://a\");")]
    [InlineData("var u = new System.Uri(\"https://a\");")]
    [InlineData("var u = new   Uri  (value);")]
    [InlineData("var u = new\tSystem.Uri\n(value);")]
    [InlineData("Uri.TryCreate(value, UriKind.Absolute, out var u);")]
    [InlineData("System.Uri.TryCreate (value, UriKind.Absolute, out var u);")]
    [InlineData("var b = new UriBuilder(value);")]
    [InlineData("var b = new System.UriBuilder { Host = value };")]
    [InlineData("var u = new global::System.Uri(value);")]
    [InlineData("var b = new global::System.UriBuilder(value);")]
    [InlineData("global::System.Uri.TryCreate(value, UriKind.Absolute, out var u);")]
    [InlineData("Uri u = new(value);")]
    [InlineData("Uri? u = new(\"https://a\");")]
    [InlineData("private static readonly Uri A = new  (value);")]
    [InlineData("public Uri A { get; } = new(value);")]
    [InlineData("client.BaseAddress = new(value);")]
    public void UriScanner_DetectsEveryWayOfBuildingAnAddress(string source)
    {
        Assert.True(CreatesUnapprovedUri("OtroArchivo.cs", source));
    }

    [Theory]
    [InlineData("var id = Guid.NewGuid();")]
    [InlineData("Uri? uri = null;")]
    [InlineData("var text = uri.ToString();")]
    [InlineData("List<string> items = new();")]
    [InlineData("var request = new HttpRequestMessage();")]
    public void UriScanner_IgnoresCodeThatBuildsNoAddress(string source)
    {
        Assert.False(CreatesUnapprovedUri("OtroArchivo.cs", source));
    }

    [Theory]
    [InlineData("ChatClientFactory.cs", "Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) && endpoint.Scheme is \"http\"", false)]
    [InlineData("ChatClientFactory.cs", "BaseAddress = new Uri(\"https://evil.example/\"),", true)]
    [InlineData("ChatClientFactory.cs", "BaseAddress = new Uri(options.ApiKey!),", true)]
    [InlineData("ChatClientFactory.cs", "BaseAddress = new Uri(options.Endpoint!),", true)]
    [InlineData("ChatClientFactory.cs", "Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var u);", true)]
    [InlineData("ChatClientFactory.cs", "var b = new UriBuilder(options.Endpoint!);", true)]
    [InlineData("AiOptionsValidator.cs", "Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is \"http\"", false)]
    [InlineData("AiOptionsValidator.cs", "Uri.TryCreate(other, UriKind.Absolute, out var uri)", true)]
    [InlineData("AiOptionsValidator.cs", "new Uri(value)", true)]
    public void UriScanner_AllowsOnlyTheExactFormsOfTheFactoryAndTheValidator(string fileName, string source, bool expected)
    {
        Assert.Equal(expected, CreatesUnapprovedUri(fileName, source));
    }

    [Fact]
    public void UriScanner_AllowsAnythingInProviderEndpoints()
    {
        Assert.False(CreatesUnapprovedUri("ProviderEndpoints.cs", "public static readonly Uri A = new Uri(\"https://a/\");"));
    }

    // Análisis sintáctico por expresión regular: cubre `new Uri(...)`/`new UriBuilder` (con `System.` o `global::System.`),
    // `Uri.TryCreate(...)` y `new(...)` de tipo inferido cuando el tipo `Uri` aparece en la misma declaración (campo, local o
    // propiedad) o se asigna a `BaseAddress`/`RequestUri`. No cubre `Uri` obtenido por otras vías (conversión desde `string` en
    // `HttpRequestMessage`, `new(...)` asignado a un destino de tipo `Uri` con otro nombre, reflexión): son límites de la guarda,
    // no garantías. La defensa real contra el SSRF es que las URL de los proveedores sean constantes (DD-3).
    private static readonly System.Text.RegularExpressions.Regex UriCreation = new(
        @"new\s*(global::)?(System\.)?Uri\s*\(|(global::)?(System\.)?Uri\.TryCreate\s*\(|new\s*(global::)?(System\.)?UriBuilder\b"
        + @"|\bUri\??\s+\w+\s*=\s*new\s*\(|\bUri\??\s+\w+\s*\{[^}]*\}\s*=\s*new\s*\(|\b(BaseAddress|RequestUri)\s*=\s*new\s*\(",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    // Formas exactas permitidas por archivo: la fábrica solo construye la dirección de Ollama a partir de Ai:Endpoint, y el
    // validador solo comprueba que ese valor sea http/https sin conservar la dirección. Cualquier otra forma es un desvío posible.
    private static readonly Dictionary<string, string> AllowedUriForms = new()
    {
        ["ChatClientFactory.cs"] = "Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)",
        ["AiOptionsValidator.cs"] = "Uri.TryCreate(value, UriKind.Absolute, out var uri)",
    };

    private static bool CreatesUnapprovedUri(string fileName, string source)
        => fileName != "ProviderEndpoints.cs"
            && UriCreation.IsMatch(AllowedUriForms.TryGetValue(fileName, out var form)
                ? source.Replace(form, string.Empty, StringComparison.Ordinal)
                : source);

    private static IEnumerable<string> SourceFiles(string directory)
        => Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, directory), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file));

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
