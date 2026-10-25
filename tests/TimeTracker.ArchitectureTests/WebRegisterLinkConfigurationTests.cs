using System.Text.Json;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// El enlace «Crear una compañía» (ADR 0033): visible por configuración en Development, oculto en el appsettings base de la Web y
/// activable en producción solo con la variable SHOW_REGISTER_LINK del servicio Web (sin recompilar), que no abre el registro de la API.
/// </summary>
public class WebRegisterLinkConfigurationTests
{
    private static JsonElement ReadWebSettings(string fileName)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Web", "wwwroot", fileName))).RootElement.Clone();

    private static string ReadFile(string relativePath) => File.ReadAllText(Path.Combine(RepositoryRoot.Path, relativePath));

    [Fact]
    public void WebDevelopmentSettings_ShowTheLink()
        => Assert.True(ReadWebSettings("appsettings.Development.json").GetProperty("Features").GetProperty("ShowRegisterLink").GetBoolean());

    [Fact]
    public void WebBaseSettings_HideTheLinkByDefault()
        => Assert.False(ReadWebSettings("appsettings.json").GetProperty("Features").GetProperty("ShowRegisterLink").GetBoolean());

    [Fact]
    public void WebConfigScript_WritesTheSwitchFromTheEnvironmentVariable_OnlyWhenItIsExactlyTrue()
    {
        var script = ReadFile("deploy/web/40-web-config.sh");

        Assert.Contains("SHOW_REGISTER_LINK", script, StringComparison.Ordinal);
        Assert.Contains("\"Features\"", script, StringComparison.Ordinal);
        Assert.Contains("\"ShowRegisterLink\"", script, StringComparison.Ordinal);
        Assert.Matches(@"(?m)^\s*true\)\s*show_register_link=true", script);
        Assert.Matches(@"(?m)^\s*\*\)\s*show_register_link=false", script);
    }

    [Fact]
    public void WebDockerfile_DoesNotEnableTheLinkByDefault()
    {
        var dockerfile = ReadFile("deploy/web/Dockerfile");

        Assert.DoesNotMatch(@"(?m)^ENV SHOW_REGISTER_LINK=\s*true", dockerfile);
    }

    [Fact]
    public void LoginPage_ShowsTheLinkOnlyThroughTheOption_AndRegisterPageLinksBack()
    {
        var login = ReadFile("src/TimeTracker.Web/Pages/Login.razor");
        var register = ReadFile("src/TimeTracker.Web/Pages/Register.razor");

        Assert.Contains("RegisterLinkOption.IsEnabled", login, StringComparison.Ordinal);
        Assert.Contains("href=\"registro\"", login, StringComparison.Ordinal);
        Assert.Contains("Crear una compañía", login, StringComparison.Ordinal);
        Assert.Contains("href=\"login\"", register, StringComparison.Ordinal);
        Assert.Contains("Volver a iniciar sesión", register, StringComparison.Ordinal);
    }

    [Fact]
    public void Adr0033_ExistsAndDocumentsTheTwoIndependentSwitches()
    {
        var adr = ReadFile("docs/adr/0033-registro-y-demostracion-en-desarrollo.md");

        foreach (var required in new[]
        {
            "Sustituye parcialmente a: ADR 0022", "ADR 0032", "SHOW_REGISTER_LINK", "Registration__Enabled", "DevelopmentInviteCode",
            "Production", "Seed:DemoUsers", "company_id", "dos",
        })
        {
            Assert.Contains(required, adr, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Readme_IndexesAdr0033_AndExplainsTheWebVariable()
    {
        var readme = ReadFile("README.md");

        Assert.Contains("(docs/adr/0033-registro-y-demostracion-en-desarrollo.md)", readme, StringComparison.Ordinal);
        Assert.Contains("SHOW_REGISTER_LINK", readme, StringComparison.Ordinal);
        Assert.Contains("Crear una compañía", readme, StringComparison.Ordinal);
    }
}
