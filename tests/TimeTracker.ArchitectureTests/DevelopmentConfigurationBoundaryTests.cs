using System.Text.Json;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// Frontera del ADR 0032: el appsettings.json base no lleva ningún valor de entorno ni secreto; solo
/// appsettings.Development.json lleva los de desarrollo, y su clave de firma no aparece en ningún artefacto de despliegue.
/// </summary>
public class DevelopmentConfigurationBoundaryTests
{
    private static readonly string[] EnvironmentSections =
        ["ConnectionStrings", "Database", "Identity", "Jwt", "Seed", "Cors", "Registration", "Ai", "Company", "ForwardedHeaders"];

    private static readonly string[] DeploymentArtifacts = [".env.example", "Dockerfile", "railway.json", "docs/runbook-railway.md"];

    private static JsonElement ReadApiSettings(string fileName)
    {
        var path = Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Api", fileName);
        Assert.True(File.Exists(path), $"No existe {fileName}.");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    [Fact]
    public void BaseAppSettings_HasNoEnvironmentValuesNorSecrets()
    {
        var present = ReadApiSettings("appsettings.json").EnumerateObject()
            .Select(property => property.Name)
            .Where(name => EnvironmentSections.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(present.Count == 0, $"appsettings.json base versiona secciones de entorno: {string.Join(", ", present)}");
    }

    [Theory]
    [InlineData("Cors:AllowedOrigins")]
    [InlineData("ConnectionStrings:Default")]
    [InlineData("Database:ApplyMigrationsOnStartup")]
    [InlineData("Jwt:SigningKey")]
    [InlineData("Seed:DemoUsers")]
    [InlineData("Seed:DemoData")]
    [InlineData("Seed:DemoPassword")]
    public void DevelopmentAppSettings_CarriesTheLocalValue(string path)
    {
        var node = ReadApiSettings("appsettings.Development.json");
        foreach (var segment in path.Split(':'))
        {
            Assert.True(node.TryGetProperty(segment, out node), $"appsettings.Development.json no define {path}.");
        }

        Assert.NotEqual(JsonValueKind.Null, node.ValueKind);
    }

    [Fact]
    public void DevelopmentAppSettings_OpenTheRegistrationWithTheDevelopmentCode_ButDoNotConfigureTheAssistant()
    {
        var settings = ReadApiSettings("appsettings.Development.json");

        // ADR 0033: en Development el registro está abierto con un código público de desarrollo (nunca en base ni en Production).
        var registration = settings.GetProperty("Registration");
        Assert.True(registration.GetProperty("Enabled").GetBoolean());
        Assert.Equal(DevelopmentInviteCodeValue, registration.GetProperty("InviteCodes").GetString());
        Assert.False(settings.TryGetProperty("Ai", out _), "Ni claves ni proveedor de IA en el repositorio.");
    }

    [Fact]
    public void BaseAppSettings_NeverOpenTheRegistration()
    {
        var settings = ReadApiSettings("appsettings.json");

        Assert.False(settings.TryGetProperty("Registration", out _), "El registro queda cerrado por defecto fuera de Development.");
    }

    [Theory]
    [InlineData(".env.example")]
    [InlineData("Dockerfile")]
    [InlineData("railway.json")]
    [InlineData("docs/runbook-railway.md")]
    [InlineData("docker-compose.yml")]
    [InlineData("README.md")]
    [InlineData("docs/adr/0033-registro-y-demostracion-en-desarrollo.md")]
    public void DevelopmentInviteCode_NeverAppearsInDeploymentArtifactsNorDocumentation(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, relativePath));

        Assert.DoesNotContain(DevelopmentInviteCodeValue, text, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentInviteCode_IsLongEnoughToPassTheRegistrationValidator()
        => Assert.True(DevelopmentInviteCodeValue.Length >= 16);

    private static string DevelopmentInviteCodeValue
        => ReadApiSettings("appsettings.Development.json").GetProperty("Registration").GetProperty("InviteCodes").GetString()!;

    [Theory]
    [InlineData(".env.example")]
    [InlineData("Dockerfile")]
    [InlineData("railway.json")]
    [InlineData("docs/runbook-railway.md")]
    [InlineData("docker-compose.yml")]
    public void DevelopmentSigningKey_NeverAppearsInDeploymentArtifacts(string relativePath)
    {
        var key = ReadApiSettings("appsettings.Development.json").GetProperty("Jwt").GetProperty("SigningKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(key));

        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, relativePath));

        Assert.DoesNotContain(key!, text, StringComparison.Ordinal);
    }

    [Fact]
    public void DeploymentArtifacts_Exist()
        => Assert.All(DeploymentArtifacts, path => Assert.True(File.Exists(Path.Combine(RepositoryRoot.Path, path)), $"Falta {path}."));

    [Fact]
    public void Adr0032_PartiallySupersedesAdr0007_OnlyForDevelopment()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs", "adr", "0032-configuracion-de-desarrollo-en-appsettings.md"));

        foreach (var required in new[] { "Sustituye parcialmente a: ADR 0007", "appsettings.Development.json", "Production", "DevelopmentSigningKey" })
        {
            Assert.Contains(required, text, StringComparison.Ordinal);
        }
    }
}
