using System.Text.RegularExpressions;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// Pruebas de texto sobre los artefactos de entrega (no hay analizador YAML en el repositorio; W1).
/// </summary>
public partial class DeliveryArtifactsTests
{
    private static readonly string[] DocumentedEnvKeys =
    [
        "POSTGRES_DB",
        "POSTGRES_USER",
        "POSTGRES_PASSWORD",
        "ConnectionStrings__Default",
        "Database__ApplyMigrationsOnStartup",
        "Jwt__SigningKey",
        "Seed__DemoUsers",
        "Seed__DemoPassword",
    ];

    // --- .env.example (SC-12, SC-13, SC-19 a SC-23) ---

    [Fact]
    public void EnvExample_ContainsTheDocumentedKeys()
    {
        var active = ActiveEnvEntries();

        foreach (var key in DocumentedEnvKeys)
        {
            Assert.True(active.ContainsKey(key), $".env.example no define {key}.");
        }

        Assert.Equal("false", active["Database__ApplyMigrationsOnStartup"]);
        foreach (var key in new[] { "POSTGRES_PASSWORD", "ConnectionStrings__Default" })
        {
            Assert.True(IsEmptyOrPlaceholder(active[key]), $"{key} debe estar vacío o ser un marcador ficticio.");
        }
    }

    [Fact]
    public void EnvExample_AuthenticationKeysAreEmptyOrFalseAndDocumented()
    {
        var text = ReadRepositoryFile(".env.example");
        var active = ActiveEnvEntries();

        // Activas (no comentadas) y vacías: quien ejecuta el proyecto las rellena en su .env local.
        Assert.Equal(string.Empty, active["Jwt__SigningKey"]);
        Assert.Equal(string.Empty, active["Seed__DemoPassword"]);
        Assert.Equal("false", active["Seed__DemoUsers"]);

        // El comentario inmediatamente anterior a la contraseña de demostración explica su uso.
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        var index = lines.FindIndex(line => line.StartsWith("Seed__DemoPassword=", StringComparison.Ordinal));
        var comment = new List<string>();
        for (var previous = index - 1; previous >= 0 && lines[previous].TrimStart().StartsWith('#'); previous--)
        {
            comment.Add(lines[previous]);
        }

        var documentation = string.Join('\n', comment);
        foreach (var required in new[] { "solo en local", "12 caracteres", "README" })
        {
            Assert.Contains(required, documentation, StringComparison.OrdinalIgnoreCase);
        }

        // El valor de demostración se documenta únicamente en el README.
        Assert.DoesNotContain("Demo-Fichajes-2026", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvExample_HasNoDevelopmentEmployeeKey()
        => Assert.DoesNotContain("Identity__DevelopmentEmployeeId", ReadRepositoryFile(".env.example"), StringComparison.Ordinal);

    [Fact]
    public void EnvExample_ListsEveryKeyConsumedByCompose()
    {
        var example = ReadRepositoryFile(".env.example");
        var consumed = ComposeVariableRegex().Matches(ReadRepositoryFile("docker-compose.yml"))
            .Select(match => match.Groups[1].Value)
            .Concat(["Cors__AllowedOrigins__0", "ASPNETCORE_HTTP_PORTS"])
            .Distinct()
            .ToList();

        // Basta con que figure, aunque sea comentada (el puerto lo fija la imagen).
        var missing = consumed.Where(key => !Regex.IsMatch(example, $@"^#?\s*{Regex.Escape(key)}=", RegexOptions.Multiline)).ToList();
        Assert.True(missing.Count == 0, $".env.example no documenta: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EnvExample_HasNoRealValues()
    {
        string[] mustBeBlank = ["Cors__AllowedOrigins__0", "POSTGRES_PASSWORD", "ConnectionStrings__Default", "Jwt__SigningKey", "Seed__DemoPassword"];
        var active = ActiveEnvEntries();

        foreach (var key in mustBeBlank.Where(active.ContainsKey))
        {
            Assert.True(IsEmptyOrPlaceholder(active[key]), $"{key} tiene un valor que no es vacío ni un marcador.");
        }

        // Ningún valor activo puede parecer un GUID ni una contraseña incrustada en una cadena de conexión.
        Assert.DoesNotContain(active.Values, value => GuidRegex().IsMatch(value) || value.Contains("Password=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnvExample_HasNoActiveAiKeys()
        => Assert.DoesNotContain(ActiveEnvEntries().Keys, key => key.StartsWith("Ai__", StringComparison.Ordinal));

    [Fact]
    public void EnvExample_DoesNotDefineEmptyPortKey()
    {
        // Un valor vacío anularía el puerto que ya fija la imagen.
        var active = ActiveEnvEntries();

        Assert.False(active.TryGetValue("ASPNETCORE_HTTP_PORTS", out var value) && value.Length == 0);
    }

    // --- docker-compose.yml (SA-1 a SA-4, PE-22) ---

    [Fact]
    public void Compose_DefinesApiAndPostgresWithRequiredSettings()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");
        var postgres = ServiceBlock(compose, "postgres");
        var api = ServiceBlock(compose, "api");

        Assert.Equal(["api", "postgres"], ServiceNames(compose).Order(StringComparer.Ordinal));
        Assert.Contains($"image: {FixtureImage()}", postgres);
        Assert.Contains("pg_isready", postgres);
        Assert.Contains("healthcheck:", postgres);
        Assert.Contains("- pgdata:/var/lib/postgresql\n", postgres.Replace("\r\n", "\n"));
        Assert.Contains("\"127.0.0.1:5432:5432\"", postgres);
        Assert.Matches(@"(?m)^volumes:\s*\r?\n\s+pgdata:", compose);
        Assert.Contains("condition: service_healthy", api);
        Assert.Contains("ASPNETCORE_ENVIRONMENT: Development", api);
        Assert.Contains("Database__ApplyMigrationsOnStartup: \"true\"", api);
        Assert.Contains("ConnectionStrings__Default: Host=postgres;", api);
        Assert.Contains("env_file: .env", api);
    }

    [Fact]
    public void Compose_PublishesApiOn5274LocalOnlyInDevelopment()
    {
        var api = ServiceBlock(ReadRepositoryFile("docker-compose.yml"), "api");

        // El puerto de host coincide con ApiBaseUrl de la Web; única publicación y solo en 127.0.0.1.
        Assert.Contains("\"127.0.0.1:5274:8080\"", api);
        Assert.Single(Regex.Matches(api, @"(?m)^\s+- ""[^""]*:\d+""\s*$"));
        Assert.Contains("ASPNETCORE_ENVIRONMENT: Development", api);
    }

    [Fact]
    public void Compose_RequiresJwtAndSeedPasswordFromEnvAndEnablesSeeding()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");
        var api = ServiceBlock(compose, "api");

        // Claves obligatorias (con mensaje si faltan) y sin ningún valor literal.
        Assert.Matches(@"(?m)^\s+Jwt__SigningKey: \$\{Jwt__SigningKey:\?[^}]+\}\s*$", api);
        Assert.Matches(@"(?m)^\s+Seed__DemoPassword: \$\{Seed__DemoPassword:\?[^}]+\}\s*$", api);
        Assert.Contains("Seed__DemoUsers: \"true\"", api);
        Assert.DoesNotContain("Identity__", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_UsesTheSameImageAsTheFixture()
        => Assert.Contains($"image: {FixtureImage()}", ServiceBlock(ReadRepositoryFile("docker-compose.yml"), "postgres"));

    [Fact]
    public void Compose_HasNoCredentialLiterals()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");
        var postgres = ServiceBlock(compose, "postgres");

        // Usuario, contraseña y base solo como referencias obligatorias a .env, y sin env_file en postgres.
        foreach (var key in new[] { "POSTGRES_DB", "POSTGRES_USER", "POSTGRES_PASSWORD" })
        {
            Assert.Matches($@"(?m)^\s+{key}: \$\{{{key}:\?[^}}]+\}}\s*$", postgres);
        }

        Assert.DoesNotContain("env_file", postgres);
        Assert.Contains("Password=${POSTGRES_PASSWORD}", compose);
    }

    // --- ci.yml (SA-21, SA-22) ---

    [Fact]
    public void Ci_RunsApplicationCoverageAfterTestsAndBeforeAudit()
    {
        var ci = ReadRepositoryFile(".github/workflows/ci.yml");
        string[] orderedSteps =
        [
            "dotnet restore",
            "dotnet format",
            "dotnet build",
            "dotnet test",
            "-Package TimeTracker.Domain -MinimumPercent 100",
            "-Package TimeTracker.Application -MinimumPercent 80",
            "package --vulnerable",
            "dotnet publish",
            "docker build",
        ];

        var positions = orderedSteps.Select(step => ci.IndexOf(step, StringComparison.Ordinal)).ToList();

        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
        Assert.DoesNotContain("push: true", ci);
        Assert.DoesNotContain("docker push", ci);
    }

    // --- ADR (SA-20, SA-21, SA-22) ---

    [Fact]
    public void Adr_Directory_HasTwelveAdrsWithFourSections()
    {
        string[] sections = ["## Contexto", "## Decisión", "## Consecuencias", "## Alternativas"];
        var adrs = Directory.GetFiles(Path.Combine(RepositoryRoot.Path, "docs", "adr"), "0*.md")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(12, adrs.Count);
        for (var index = 0; index < adrs.Count; index++)
        {
            var name = Path.GetFileName(adrs[index]);
            var text = File.ReadAllText(adrs[index]);

            Assert.StartsWith($"{index + 1:0000}-", name, StringComparison.Ordinal);
            Assert.Matches(@"(?m)^- Estado: \S", text);
            Assert.Matches(@"(?m)^- Fecha: \d{4}-\d{2}-\d{2}", text);
            var missing = sections.Where(section => !text.Contains(section, StringComparison.Ordinal)).ToList();
            Assert.True(missing.Count == 0, $"{name} no tiene las secciones: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void Adr0009_IsSupersededBy0011_AndAdr0011StatesItSupersedesIt()
    {
        var adr0009 = ReadRepositoryFile("docs/adr/0009-identidad-provisional.md");
        var adr0011 = ReadRepositoryFile("docs/adr/0011-autenticacion-jwt-roles-y-equipo.md");

        Assert.Contains("- Estado: Sustituido por 0011", adr0009, StringComparison.Ordinal);
        Assert.Matches(@"(?m)^- Estado: .*sustituye al 0009", adr0011);
    }

    [Fact]
    public void Adr0011And0012_RecordTheirRequiredConsequences()
    {
        var adr0011 = ReadRepositoryFile("docs/adr/0011-autenticacion-jwt-roles-y-equipo.md");
        var adr0012 = ReadRepositoryFile("docs/adr/0012-sesion-web-y-riesgo-xss.md");

        string[] required0011 = ["refresh token", "AGENTS", "clave foránea", "time_entries", "Disparador", "HMAC-SHA512", "100.000", "ManagerId"];
        string[] required0012 = ["sessionStorage", "XSS", "localStorage", "HttpOnly"];

        var missing = required0011.Where(text => !adr0011.Contains(text, StringComparison.Ordinal)).Select(text => $"0011:{text}")
            .Concat(required0012.Where(text => !adr0012.Contains(text, StringComparison.Ordinal)).Select(text => $"0012:{text}"))
            .ToList();

        Assert.True(missing.Count == 0, $"Los ADR no mencionan: {string.Join(", ", missing)}");
    }

    // --- README y config.yaml (SA-23, SA-24, SA-25, PE-14) ---

    [Fact]
    public void Readme_DocumentsAuthenticationUsersAndVariables()
    {
        string[] required =
        [
            "docker compose up",
            "docker compose down -v",
            "/login",
            "/fichajes",
            "/equipo",
            "5274",
            "requieren Docker",
            "ConnectionStrings__Default",
            "ASPNETCORE_ENVIRONMENT",
            "Jwt__SigningKey",
            "Seed__DemoUsers",
            "Seed__DemoPassword",
            "Cors__AllowedOrigins__0=http://localhost:5172",
            "RandomNumberGenerator",
            "marta.responsable@example.test",
            "luis.empleado@example.test",
            "ana.empleada@example.test",
            "pablo.empleado@example.test",
            "Demo-Fichajes-2026",
            "despliegue real",
            "dotnet ef",
            "0011-autenticacion-jwt-roles-y-equipo.md",
            "0012-sesion-web-y-riesgo-xss.md",
        ];

        var readme = ReadRepositoryFile("README.md");
        var missing = required.Where(text => !readme.Contains(text, StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, $"README.md no menciona: {string.Join(", ", missing)}");
        Assert.DoesNotContain("Identity__DevelopmentEmployeeId", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigYaml_ReflectsAuthenticationAndDropsTheProvisionalNote()
    {
        var config = ReadRepositoryFile("openspec/config.yaml");

        string[] required = ["autenticacion-y-roles", "JWT", "Employee", "Manager", "ADR 0008-0012"];
        var missing = required.Where(text => !config.Contains(text, StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, $"config.yaml no menciona: {string.Join(", ", missing)}");
        Assert.DoesNotContain("Identidad provisional (solo Development", config, StringComparison.Ordinal);
        Assert.DoesNotContain("Identity__DevelopmentEmployeeId", config, StringComparison.Ordinal);
    }

    // --- Utilidades ---

    private static string ReadRepositoryFile(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot.Path, relativePath);
        Assert.True(File.Exists(path), $"No existe {relativePath}.");
        return File.ReadAllText(path);
    }

    private static Dictionary<string, string> ActiveEnvEntries()
        => ReadRepositoryFile(".env.example").Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#') && line.Contains('='))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private static bool IsEmptyOrPlaceholder(string value)
        => value.Length == 0 || value.StartsWith("cambiar", StringComparison.OrdinalIgnoreCase);

    // La imagen de compose debe ser la misma que usa el fixture de pruebas.
    private static string FixtureImage()
    {
        var fixture = ReadRepositoryFile("tests/TimeTracker.Api.IntegrationTests/Postgres/PostgresFixture.cs");
        return FixtureImageRegex().Match(fixture).Groups[1].Value;
    }

    // Servicios = claves de segundo nivel bajo «services:».
    private static List<string> ServiceNames(string compose)
    {
        var services = Regex.Match(compose, @"(?ms)^services:\s*$(.*?)(?=^\S|\z)").Groups[1].Value;
        return [.. Regex.Matches(services, @"(?m)^  ([a-z][\w-]*):\s*$").Select(match => match.Groups[1].Value)];
    }

    // Texto del servicio: desde «  nombre:» hasta el siguiente servicio o la siguiente clave de primer nivel.
    private static string ServiceBlock(string compose, string name)
    {
        var match = Regex.Match(compose, $@"(?ms)^  {Regex.Escape(name)}:\s*$(.*?)(?=^  [a-z][\w-]*:\s*$|^\S|\z)");
        Assert.True(match.Success, $"docker-compose.yml no define el servicio {name}.");
        return match.Groups[1].Value;
    }

    [GeneratedRegex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex ComposeVariableRegex();

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"Image\s*=\s*""([^""]+)""")]
    private static partial Regex FixtureImageRegex();
}
