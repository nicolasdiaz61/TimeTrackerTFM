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

    private static readonly string[] AiEnvKeys =
    [
        "Ai__Provider",
        "Ai__Model",
        "Ai__Endpoint",
        "Ai__ApiKey",
        "Ai__MaxOutputTokens",
        "Ai__MaxToolIterations",
        "Ai__TimeoutSeconds",
        "Ai__RequestsPerMinute",
        "Ai__DailyRequestsPerUser",
    ];

    [Fact]
    public void EnvExample_DocumentsTheNineAiKeysWithOnlyProviderAndApiKeyActiveAndEmpty()
    {
        var example = ReadRepositoryFile(".env.example");
        var active = ActiveEnvEntries();

        foreach (var key in AiEnvKeys)
        {
            Assert.Matches($@"(?m)^#?\s*{key}=", example);
        }

        // Activas y vacías (asistente deshabilitado por defecto); las otras siete, comentadas.
        Assert.Equal(["Ai__ApiKey", "Ai__Provider"], active.Keys.Where(key => key.StartsWith("Ai__", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.Equal(string.Empty, active["Ai__Provider"]);
        Assert.Equal(string.Empty, active["Ai__ApiKey"]);
        Assert.Contains("# Ai__Endpoint=http://host.docker.internal:11434/", example, StringComparison.Ordinal);
        foreach (var (key, value) in new[] { ("MaxOutputTokens", "800"), ("MaxToolIterations", "5"), ("TimeoutSeconds", "25"), ("RequestsPerMinute", "5"), ("DailyRequestsPerUser", "50") })
        {
            Assert.Contains($"# Ai__{key}={value}", example, StringComparison.Ordinal);
        }
    }

    // 4b.12 (SC «Documentación en .env.example», parte del asistente): topes por compañía, comentados y con su valor por defecto.
    [Theory]
    [InlineData("Ai__RequestsPerMinutePerCompany", "30")]
    [InlineData("Ai__DailyRequestsPerCompany", "500")]
    [InlineData("Ai__MaxLoadedCompanies", "100")]
    [InlineData("Ai__RuntimeIdleMinutes", "60")]
    public void EnvExample_DocumentsTheCompanyLimitsCommentedWithTheirDefaults(string key, string defaultValue)
    {
        var example = ReadRepositoryFile(".env.example");

        Assert.Matches($@"(?m)^# {key}={defaultValue}\s*$", example);
        Assert.False(ActiveEnvEntries().ContainsKey(key), $"{key} no debe estar activa en .env.example.");
    }

    // 5.12 (SC «Documentación en .env.example de las claves multi-compañía»): el registro cerrado es la línea activa; los códigos y el tope
    // de personas, comentados y sin ningún valor secreto.
    [Fact]
    public void EnvExample_KeepsTheRegistrationClosedByDefaultAsTheOnlyActiveLine()
    {
        var active = ActiveEnvEntries();

        Assert.Equal("false", active["Registration__Enabled"]);
        Assert.False(active.ContainsKey("Registration__InviteCodes"), "Registration__InviteCodes no debe estar activa en .env.example.");
        Assert.False(active.ContainsKey("Company__MaxEmployees"), "Company__MaxEmployees no debe estar activa en .env.example.");
    }

    [Fact]
    public void EnvExample_DocumentsTheInviteCodesCommentedWithoutAValue_AndTheEmployeeCapWithItsDefault()
    {
        var example = ReadRepositoryFile(".env.example");

        Assert.Matches(@"(?m)^# Registration__InviteCodes=\s*$", example);
        Assert.Matches(@"(?m)^# Company__MaxEmployees=50\s*$", example);
        // Ninguna línea, activa o comentada, asigna un valor a los códigos ni a las claves maestras.
        Assert.DoesNotMatch(@"(?m)^#?\s*Registration__InviteCodes=\S", example);
        Assert.DoesNotMatch(@"(?m)^#?\s*Ai__MasterKeys__\d+=\S", example);
    }

    [Fact]
    public void EnvExample_ExplainsTheRegistrationKeys()
    {
        var comments = string.Join('\n', ReadRepositoryFile(".env.example").Split('\n').Where(line => line.TrimStart().StartsWith('#')));

        string[] required =
        [
            "cerrado por defecto", "aislamiento", "probado y desplegado", "secretos", "únicamente en el entorno del despliegue (Railway)",
            "nunca en este archivo", "Seed__DemoUsers=true", "se fijan por entorno", "no los edita un Manager", "clave maestra", "solo en Railway",
        ];
        var missing = required.Where(text => !comments.Contains(text, StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, $".env.example no explica: {string.Join(", ", missing)}");
    }

    [Fact]
    public void DockerCompose_NeitherOpensTheRegistrationNorDefinesInviteCodes()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");

        Assert.DoesNotContain("Registration__", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("InviteCodes", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvExample_SaysThatAManagerDoesNotEditTheCompanyLimits()
    {
        var comments = string.Join('\n', ReadRepositoryFile(".env.example").Split('\n').Where(line => line.TrimStart().StartsWith('#')));

        Assert.Contains("no las edita un Manager", comments, StringComparison.Ordinal);
        Assert.Contains("Europe/Madrid", comments, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvExample_ExplainsTheDisabledAssistantProvidersAndTheSecretKey()
    {
        var comments = string.Join('\n', ReadRepositoryFile(".env.example").Split('\n').Where(line => line.TrimStart().StartsWith('#')));

        Assert.Contains("deshabilitado", comments, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Anthropic", comments, StringComparison.Ordinal);
        Assert.Contains("Ollama", comments, StringComparison.Ordinal);
        Assert.Contains("SECRETO", comments, StringComparison.Ordinal);
        Assert.Contains("nunca se escribe", comments, StringComparison.Ordinal);
    }

    private static readonly string[] ManagerConfigEnvKeys =
    [
        "Ai__Effort",
        "Ai__AllowedProviders",
        "Ai__MasterKeys__1",
        "Ai__ActiveMasterKeyVersion",
        "Ai__SettingsPollSeconds",
    ];

    [Fact]
    public void EnvExample_DocumentsTheManagerConfigKeysAsComments()
    {
        var example = ReadRepositoryFile(".env.example");

        foreach (var key in ManagerConfigEnvKeys)
        {
            Assert.Matches($@"(?m)^#\s*{key}=", example);
            Assert.DoesNotMatch($@"(?m)^{key}=", example);
        }
    }

    [Fact]
    public void EnvExample_NeverCarriesAMasterKeyValue()
        => Assert.DoesNotMatch(@"(?m)^#?\s*Ai__MasterKeys__\d+[ \t]*=[ \t]*[^\s]", ReadRepositoryFile(".env.example"));

    [Fact]
    public void EnvExample_ExplainsHowToGenerateTheMasterKey()
        => Assert.Contains("32 bytes aleatorios en Base64", ReadRepositoryFile(".env.example"), StringComparison.Ordinal);

    [Fact]
    public void EnvExample_ListsEverySupportedProvider()
    {
        var comments = string.Join('\n', ReadRepositoryFile(".env.example").Split('\n').Where(line => line.TrimStart().StartsWith('#')));

        foreach (var provider in new[] { "Anthropic", "OpenAI", "Gemini", "Ollama" })
        {
            Assert.Contains(provider, comments, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DeliveryFiles_NeverCarryAnApiKeyValue()
    {
        foreach (var file in new[] { ".env.example", "docker-compose.yml", "README.md" })
        {
            // Solo espacios y tabuladores tras el signo: un salto de línea seguido de otro comentario no es un valor.
            Assert.DoesNotMatch(@"Ai__ApiKey[ \t]*[=:][ \t]*[^\s`|]", ReadRepositoryFile(file));
        }
    }

    [Fact]
    public void Compose_LetsTheApiReachAnOllamaOnTheHost()
        => Assert.Contains("\"host.docker.internal:host-gateway\"", ServiceBlock(ReadRepositoryFile("docker-compose.yml"), "api"), StringComparison.Ordinal);

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
    public void Adr_Directory_HasTwentyTwoAdrsWithFourSections()
    {
        string[] sections = ["## Contexto", "## Decisión", "## Consecuencias", "## Alternativas"];
        var adrs = Directory.GetFiles(Path.Combine(RepositoryRoot.Path, "docs", "adr"), "0*.md")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(22, adrs.Count);
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
    public void Adr0017_RecordsTheAssistantDecisionAndItsConsequences()
    {
        var adr = ReadRepositoryFile("docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md");

        string[] required =
        [
            "Infrastructure", "alternativa A", "solo lectura", "RGPD", "proveedor en la nube", "datos inventados",
            "en memoria", "se reinicia con cada despliegue", "no se comparte entre réplicas", "presupuesto",
            "DD-21", "después de la autorización", "sin `Ai:Provider`", "`503`", "B. IA en Application",
        ];
        var missing = required.Where(text => !adr.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.True(missing.Count == 0, $"El ADR 0017 no menciona: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Adr0018_RecordsTheManagerConfigurationDecisionAndWhatItAmends()
    {
        var adr = ReadRepositoryFile("docs/adr/0018-configuracion-del-asistente-por-el-responsable.md");

        string[] required =
        [
            "AGENTS", "§1", "§7", "ADR 0017", "AES-256-GCM", "clave maestra", "solo escritura", "solo inserción",
            "xmin", "datos personales", "rol obsoleto", "GitHub Models", "OpenCode Zen", "Data Protection", "Ai__AllowedProviders",
        ];
        var missing = required.Where(text => !adr.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.True(missing.Count == 0, $"El ADR 0018 no menciona: {string.Join(", ", missing)}");
    }

    [Fact]
    public void AgentsMd_AllowsStoredConfigurationForTheAssistantProviderAndPointsToAdr0021()
    {
        var agents = ReadRepositoryFile("AGENTS.md");

        Assert.Contains("configuración de entorno o guardada por un Manager de su compañía (ADR 0021)", agents, StringComparison.Ordinal);
        Assert.Contains("configuración guardada", agents, StringComparison.Ordinal);
        Assert.DoesNotContain("guardada por un Manager (ADR 0018)", agents, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_ExplainsTheManagerAssistantConfigurationAndKeyRotation()
    {
        var readme = ReadRepositoryFile("README.md");

        string[] required =
        [
            "/asistente/configuracion", "Ai__MasterKeys__1", "Ai__ActiveMasterKeyVersion", "Ai__AllowedProviders", "Ai__Effort",
            "OpenAI", "Gemini", "rotación", "datos personales", "0018-configuracion-del-asistente-por-el-responsable.md",
        ];
        var missing = required.Where(text => !readme.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.True(missing.Count == 0, $"README.md no menciona: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Readme_RailwaySection_ExplainsTheMasterKeyOrderTheCorsOriginAndTheConfigurationRisks()
    {
        var readme = ReadRepositoryFile("README.md");
        var start = readme.IndexOf("## Despliegue en Railway", StringComparison.Ordinal);
        var end = readme.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Falta la sección «Despliegue en Railway».");
        var railway = readme[start..end];

        string[] required =
        [
            "Ai__MasterKeys__1", "Ai__ActiveMasterKeyVersion=1", "antes de que un responsable guarde", "Cors__AllowedOrigins__0",
            "obligatoria", "Responses", "datos personales", "tercero", "custodia", "product owner", "Ai__AllowedProviders",
        ];
        var missing = required.Where(text => !railway.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(missing.Count == 0, $"La sección de Railway no menciona: {string.Join(", ", missing)}");

        // La clave maestra es un secreto: se define por stdin y el documento nunca le asigna un valor.
        Assert.Contains("railway variables set Ai__MasterKeys__1 --stdin -s api", railway, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"Ai__MasterKeys__\d+\s*=\s*[^\s`|]", railway);
    }

    [Fact]
    public void Readme_DoesNotAnnounceAsFutureThePagesThatAlreadyExist()
    {
        var readme = ReadRepositoryFile("README.md");

        Assert.DoesNotContain("llegará en un cambio posterior", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("podrá elegir", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvExample_DescribesTheAllowedProvidersDefaultAndFormatWithoutClaimingAllAreAllowed()
    {
        var comments = string.Join('\n', ReadRepositoryFile(".env.example").Split('\n').Where(line => line.TrimStart().StartsWith('#')));

        Assert.DoesNotContain("Vacío = todos", comments, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("solo está permitido el proveedor del entorno", comments, StringComparison.Ordinal);
        Assert.Contains("No distingue mayúsculas", comments, StringComparison.Ordinal);
        Assert.Contains("espacios", comments, StringComparison.Ordinal);
        Assert.Contains("GitHubModels", comments, StringComparison.Ordinal);
        Assert.Contains("None", comments, StringComparison.Ordinal);
    }

    [Fact]
    public void Adr0019_AndTheMigration_DoNotClaimTheTriggerStopsTheTableOwner()
    {
        var adr = ReadRepositoryFile("docs/adr/0019-auditoria-inmutable-y-despliegue-de-la-configuracion-del-asistente.md");
        var migration = ReadRepositoryFile("src/TimeTracker.Infrastructure/Persistence/Migrations/20261020111927_AddAssistantAuditImmutability.cs");

        Assert.Contains("DISABLE TRIGGER", adr, StringComparison.Ordinal);
        Assert.Contains("DROP TRIGGER", adr, StringComparison.Ordinal);
        Assert.Contains("DISABLE TRIGGER", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("pero el trigger sí.", adr, StringComparison.Ordinal);
        Assert.DoesNotContain("el trigger sí.", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Adr0019_ComplementsAdr0018WithTheDatabaseGuardTheRailwayDeploymentAndTheOpenKeyCustodyQuestion()
    {
        var adr = ReadRepositoryFile("docs/adr/0019-auditoria-inmutable-y-despliegue-de-la-configuracion-del-asistente.md");

        string[] required =
        [
            "ADR 0018", "matiza", "Railway", "Ai__MasterKeys__1", "--stdin", "Ai__ActiveMasterKeyVersion=1", "Cors__AllowedOrigins__0",
            "Responses", "datos personales", "tercero", "custodia", "pregunta abierta", "product owner", "trigger", "UPDATE", "DELETE",
            "TRUNCATE", "REVOKE", "configuración única", "cualquier Manager",
        ];
        var missing = required.Where(text => !adr.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(missing.Count == 0, $"El ADR 0019 no menciona: {string.Join(", ", missing)}");
        Assert.DoesNotMatch(@"Ai__MasterKeys__\d+\s*=\s*[^\s`|]", adr);
    }

    [Fact]
    public void Adr0018_IsKeptAsMergedBecauseAdrsAreNotRewritten()
    {
        var adr = ReadRepositoryFile("docs/adr/0018-configuracion-del-asistente-por-el-responsable.md");

        Assert.DoesNotContain("AddAssistantAuditImmutability", adr, StringComparison.Ordinal);
        Assert.DoesNotContain("ADR 0019", adr, StringComparison.Ordinal);
        Assert.Contains("Disparador de inmutabilidad en la base de datos para la auditoría:", adr, StringComparison.Ordinal);
    }


    // --- ADR 0020 y 0021 (multi-compania, PR 4a; SA «Decisiones y límites documentados») ---

    // Huellas SHA-256 (con saltos de línea normalizados a LF) de los ADR ya fusionados: no se reescriben (ADR 0001, AGENTS §8).
    // El 0021 los sustituye parcialmente sin editarlos; si una huella cambia, alguien ha editado un ADR aceptado.
    private static readonly Dictionary<string, string> MergedAdrFingerprints = new()
    {
        ["docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md"] = "43ee578236edf120db2c0d0d2a50d162f99bf2bee9b4541fe55b26e45690c132",
        ["docs/adr/0018-configuracion-del-asistente-por-el-responsable.md"] = "a6634dfb393ab5588885111ab6c8044ec933532432f09c66a626af7ee3829576",
        ["docs/adr/0019-auditoria-inmutable-y-despliegue-de-la-configuracion-del-asistente.md"] = "834f1135d89b1d93617f26fbc79ed4a835cece101210e0530bb796149b0ffe1e",
    };

    [Fact]
    public void Adr0020_RecordsTheMultiCompanyIsolationDecisionAndItsLimits()
    {
        var adr = ReadRepositoryFile("docs/adr/0020-aislamiento-multiempresa.md");

        string[] required =
        [
            "NO ACTION", "RESTRICT", "23503", "23001", "compañía por defecto", "00000000-0000-7000-8000-000000000001", "uses_platform_assistant",
            "cid", "cname", "30 minutos", "RLS", "propietario", "dos roles", "FORCE ROW LEVEL SECURITY", "app.company_id", "interceptor", "pool",
            "enumeración de correos", "cualquier Manager", "localizadores de servicios", "AGENTS §3", "ICompanyScopeFactory.CreateScope",
            "contexto ambiental", "lista cerrada", "CreateScopeClosedListTests", "IgnoreQueryFilters", "UserRepository.FindByEmailAsync",
            "AssistantSettingsStore.ListVersionsAsync", "un rollback de código no revierte el esquema", "rollback de un clic", "Railway", "Down",
            "fix-forward", "baja de usuarios", "RGPD", "v1", "recifra", "ADR 0011", "expandir", "contraer", "404",
        ];
        var missing = required.Where(text => !adr.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(missing.Count == 0, $"El ADR 0020 no menciona: {string.Join(", ", missing)}");
        Assert.DoesNotMatch(@"Ai__MasterKeys__\d+\s*=\s*[^\s`|]", adr);
    }

    [Fact]
    public void Adr0021_PartiallySupersedesAdr0018And0019OnlyOnTheSingleRowScope_AndRecordsThePerCompanyDecisions()
    {
        var adr = ReadRepositoryFile("docs/adr/0021-configuracion-del-asistente-por-compania.md");

        string[] required =
        [
            "ADR 0018", "ADR 0019", "sustituye parcialmente", "fila única", "toda la instancia", "cualquier Manager de cualquier equipo",
            "fila por compañía", "instantáneas por compañía", "sondeo global", "company_id", "deshabilitado", "uses_platform_assistant",
            "Ai__ApiKey", "AAD v2", "sin guardar", "mejora no bloqueante", "Ai__AllowedProviders", "lista de plataforma", "Ollama",
            "puente transitorio", "revertir", "volver a introducir la clave", "ADR 0001",
        ];
        var missing = required.Where(text => !adr.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(missing.Count == 0, $"El ADR 0021 no menciona: {string.Join(", ", missing)}");
        Assert.DoesNotMatch(@"Ai__MasterKeys__\d+\s*=\s*[^\s`|]", adr);
        Assert.Matches(@"(?m)^- Sustituye parcialmente a: ", adr);
    }

    // --- ADR 0022 (multi-compania, PR 5; CR y SC) ---

    [Fact]
    public void Adr0022_RecordsTheRegistrationAndPasswordDecisionsAndTheirAcceptedRisks()
    {
        var adr = ReadRepositoryFile("docs/adr/0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md");

        string[] required =
        [
            "código de invitación", "reutilizable", "rotable", "Registration__Enabled", "cerrado por defecto", "tiempo constante", "SHA-256",
            "5 por hora", "Hops", "ForwardedHeaders", "enumeración", "409", "12", "128", "reglas de composición", "contraseña temporal", "un solo uso",
            "cambio forzado", "regenerar", "responsable directo", "correo único", "RGPD", "verificación de correo", "usuarios de demostración",
            "SkipCompanyContext", "CreateScope", "no se registra", "sustituir la lista completa",
        ];
        var missing = required.Where(text => !adr.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(missing.Count == 0, $"El ADR 0022 no menciona: {string.Join(", ", missing)}");
        Assert.DoesNotMatch(@"Registration__InviteCodes\s*=\s*[^\s`|]", adr);
        Assert.DoesNotMatch(@"Ai__MasterKeys__\d+\s*=\s*[^\s`|]", adr);
    }

    [Fact]
    public void Adr0022_RecordsThePr6DecisionsOnTemporaryPasswordsTheCapAndTheAcceptedRisks()
    {
        var adr = ReadRepositoryFile("docs/adr/0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md");

        string[] required =
        [
            "POST /api/employees", "temporary-password", "change-password", "no-store", "RandomNumberGenerator", "PasswordChangePendingMiddleware", "mcp",
            "xmin", "Serializable", "40001", "3", "Company__MaxEmployees", "cerrojo en memoria", "no caduca", "no se invalida", "30 por minuto",
            "5 por minuto", "responsable directo", "revisar periódicamente",
        ];
        var missing = required.Where(text => !adr.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(missing.Count == 0, $"El ADR 0022 no menciona: {string.Join(", ", missing)}");
        // La política de contraseñas del ADR ya no dice que el cambio de contraseña «llega» después: está aplicada.
        Assert.Contains("el cambio de contraseña (`POST /api/auth/change-password`, PR 6", adr, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"temporaryPassword\s*[:=]\s*""[^""]+""", adr);
    }

    [Fact]
    public void Readme_IndexesAdr0022()
        => Assert.Contains("(docs/adr/0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md)", ReadRepositoryFile("README.md"), StringComparison.Ordinal);

    [Fact]
    public void MergedAdrs_AreNotEdited_BecauseTheNewAdrsAmendThemWithoutRewritingThem()
    {
        foreach (var (path, expected) in MergedAdrFingerprints)
        {
            var normalized = ReadRepositoryFile(path).Replace("\r\n", "\n", StringComparison.Ordinal);
            var actual = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)));

            Assert.True(expected == actual, $"{path} se ha editado: los ADR aceptados no se reescriben, se sustituyen por otro nuevo (ADR 0001).");
        }
    }

    [Fact]
    public void Readme_IndexesAdr0020And0021_AndMarksTheAdrsThat0021PartiallySupersedes()
    {
        var readme = ReadRepositoryFile("README.md");

        Assert.Contains("(docs/adr/0020-aislamiento-multiempresa.md)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/adr/0021-configuracion-del-asistente-por-compania.md)", readme, StringComparison.Ordinal);
        var lines = readme.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var adr in new[] { "0018-configuracion-del-asistente-por-el-responsable", "0019-auditoria-inmutable-y-despliegue" })
        {
            var line = lines.Single(candidate => candidate.Contains($"docs/adr/{adr}", StringComparison.Ordinal) && candidate.TrimStart().Length > 0 && char.IsDigit(candidate.TrimStart()[0]));
            Assert.Contains("sustituido parcialmente por el 0021", line, StringComparison.Ordinal);
        }
    }
    [Fact]
    public void Readme_ExplainsTheAssistant()
    {
        var readme = ReadRepositoryFile("README.md");

        string[] required =
        [
            "## Asistente de IA", "solo lectura", "Ai__Provider", "Anthropic", "Ollama", "Ai__Endpoint", "Ai__ApiKey",
            "secreto", "/asistente", "datos personales", "no llaman a ningún modelo real", "0017-asistente-ia-con-herramientas-de-solo-lectura.md",
        ];
        var missing = required.Where(text => !readme.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.True(missing.Count == 0, $"README.md no menciona: {string.Join(", ", missing)}");
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

    // --- PR 8 (multi-compania): AGENTS.md, README, runbook, specs vigentes y cierre de V-n (SA «Decisiones y límites documentados») ---

    private static string Section(string text, string startMarker, string endMarker)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Falta la sección «{startMarker}».");
        var end = text.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Falta el final «{endMarker}» de la sección «{startMarker}».");
        return text[start..end];
    }

    private static List<string> Missing(string text, IEnumerable<string> required)
        => [.. required.Where(item => !text.Contains(item, StringComparison.Ordinal))];

    [Fact]
    public void AgentsMd_Section1_SaysOneInstanceServesSeveralCompaniesAndTheAssistantIsConfiguredPerCompany()
    {
        var agents = ReadRepositoryFile("AGENTS.md");
        var section = Section(agents, "## 1. Proyecto", "## 2.");

        var missing = Missing(section, ["varias compañías", "`company_id`", "ADR 0020", "por compañía", "ADR 0021"]);
        Assert.True(missing.Count == 0, $"AGENTS.md §1 no menciona: {string.Join(", ", missing)}");
        Assert.DoesNotContain("toda la instancia", agents, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("configuración única", agents, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentsMd_Section6_FixesTheCompanyIsolationRules()
    {
        var section = Section(ReadRepositoryFile("AGENTS.md"), "## 6. Seguridad", "## 7.");

        string[] required =
        [
            "`CompanyId` sale siempre del claim `cid`", "ICurrentUser.CompanyId", "nunca de la ruta, la consulta, el cuerpo ni una cabecera",
            "filtro global", "`IgnoreQueryFilters` solo en la lista cerrada", "`CreateScope` solo desde su lista cerrada", "dos compañías",
            "12 a 128 caracteres", "sin reglas de composición", "contraseña temporal", "un solo uso",
        ];
        var missing = Missing(section, required);
        Assert.True(missing.Count == 0, $"AGENTS.md §6 no menciona: {string.Join(", ", missing)}");
    }

    [Fact]
    public void AgentsMd_Section7_DescribesPerCompanyConfigurationMigrationsRollbackAndTheManualDeployment()
    {
        var section = Section(ReadRepositoryFile("AGENTS.md"), "## 7. Infraestructura y entrega", "## 8.");

        string[] required =
        [
            "guardada por un Manager de su compañía (ADR 0021)", "expandir/contraer", "un rollback de código no revierte el esquema", "`Down` antes de redesplegar",
            "`company_id` con clave foránea compuesta y filtro global", "`railway up`", "sin repositorio conectado", "el plan de despliegue por fases debe rehacerse",
        ];
        var missing = Missing(section, required);
        Assert.True(missing.Count == 0, $"AGENTS.md §7 no menciona: {string.Join(", ", missing)}");
        Assert.DoesNotContain("(ADR 0018)", section, StringComparison.Ordinal);
    }

    private static readonly string[] RunbookRequired =
    [
        "pg_dump -Fc", "ON_ERROR_STOP=1", "--single-transaction", "`railway up`", "sin repositorio conectado", "Database__ApplyMigrationsOnStartup",
        "Registration__Enabled", "Registration__InviteCodes", "Company__MaxEmployees", "ForwardedHeaders__Enabled", "Hops", "/health",
        "ensayo", "`Up A`", "`Down A`", "`Up B`", "`Down B`", "`Down` B → `Down` A", "ApplyMigrationsOnStartup=false", "corrección hacia delante",
        "rollback de un clic", "pérdida de la separación", "sustituye la lista completa", "Seed__DemoUsers=false",
        "must_change_password", "no caduca", "`mcp`", "30 minutos", "Ai__MasterKeys__1", "custodia", "/64", "V-3", "V-6", "V-7", "pendiente",
    ];

    [Fact]
    public void Runbook_ExistsAndCoversBackupRehearsalRollbackFixForwardAndTheOperatorProcesses()
    {
        var runbook = ReadRepositoryFile("docs/runbook-railway.md");

        var missing = Missing(runbook, RunbookRequired);
        Assert.True(missing.Count == 0, $"El runbook no menciona: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Runbook_OpensTheRegistrationInTheDocumentedOrder()
    {
        var runbook = ReadRepositoryFile("docs/runbook-railway.md");
        var section = Section(runbook, "## Apertura del registro", "\n## ");

        string[] ordered =
        [
            "`Seed__DemoUsers=false`", "no se recrean", "retirar los usuarios demo", "`Registration__InviteCodes`", "`Registration__Enabled=true`",
        ];
        var positions = ordered.Select(step => section.IndexOf(step, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
    }

    [Fact]
    public void Runbook_SaysThatTheProductionChecksOfTheOperatorAreStillPendingAndDoesNotClaimThemAsDone()
    {
        var runbook = ReadRepositoryFile("docs/runbook-railway.md");
        var section = Section(runbook, "## Verificaciones de producción", "\n## ");

        foreach (var check in new[] { "V-3", "V-6", "V-7" })
        {
            var line = section.Split('\n').Single(candidate => candidate.StartsWith($"- **{check}", StringComparison.Ordinal));
            Assert.Contains("Pendiente del responsable", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Readme_ExplainsTheRegistrationTheTemporaryPasswordTheMasterKeyAndLinksTheRunbook()
    {
        var readme = ReadRepositoryFile("README.md");

        string[] required =
        [
            "## Compañías, registro y alta de personas", "`Registration__Enabled`", "`Registration__InviteCodes`", "código de invitación", "cerrado por defecto",
            "`/registro`", "contraseña temporal", "una sola vez", "`/equipo/alta`", "`Company__MaxEmployees`", "`/cambiar-contrasena`",
            "La clave maestra vive solo en Railway", "docs/runbook-railway.md", "/64", "ForwardedHeaders", "seguimiento",
        ];
        var missing = Missing(readme, required);
        Assert.True(missing.Count == 0, $"README.md no menciona: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Readme_NoLongerSaysTheAssistantConfigurationIsForTheWholeCompanyInstanceOrThatTheAutoDeployIsActive()
    {
        var readme = ReadRepositoryFile("README.md");

        Assert.DoesNotContain("vale para toda la empresa", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("afecta a toda la empresa", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("Configuración única.", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("### Activar el autodespliegue desde GitHub", readme, StringComparison.Ordinal);
        Assert.Contains("no tienen repositorio conectado", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_IndexesEveryAdrInTheDirectory()
    {
        var readme = ReadRepositoryFile("README.md");

        foreach (var adr in Directory.GetFiles(Path.Combine(RepositoryRoot.Path, "docs", "adr"), "0*.md").Select(Path.GetFileName))
        {
            Assert.Contains($"(docs/adr/{adr})", readme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DeliveryDocuments_NeverCarryASecretAnInviteCodeOrASessionLink()
    {
        string[] files =
        [
            "AGENTS.md", "README.md", ".env.example", "docker-compose.yml", "docs/runbook-railway.md",
            "docs/adr/0020-aislamiento-multiempresa.md", "docs/adr/0021-configuracion-del-asistente-por-compania.md",
            "docs/adr/0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md",
            "openspec/changes/multi-compania/design.md", "openspec/changes/multi-compania/verify-report.md",
        ];

        foreach (var file in files)
        {
            var text = ReadRepositoryFile(file);
            Assert.DoesNotMatch(@"Registration__InviteCodes[ \t]*[=:][ \t]*[^\s`|]", text);
            Assert.DoesNotMatch(@"Ai__MasterKeys__\d+[ \t]*[=:][ \t]*[^\s`|]", text);
            Assert.DoesNotMatch(@"Ai__ApiKey[ \t]*[=:][ \t]*[^\s`|]", text);
            Assert.DoesNotMatch(@"\bsk-[A-Za-z0-9_-]{16,}", text);
            Assert.DoesNotMatch(@"[A-Za-z0-9+/]{43}=", text);
            Assert.DoesNotContain("claude.ai/code/session", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SolutionArchitectureSpec_NoLongerClaimsThereAreOnlySeventeenAdrs()
    {
        var spec = ReadRepositoryFile("openspec/specs/solution-architecture/spec.md");

        Assert.DoesNotContain("diecisiete ADR", spec, StringComparison.Ordinal);
        Assert.DoesNotContain("los ADR 0001 a 0017:", spec, StringComparison.Ordinal);
        Assert.DoesNotContain("(13 ADR y README)", spec, StringComparison.Ordinal);
        Assert.Contains("ADR 0001 a 0022", spec, StringComparison.Ordinal);
        Assert.Contains("veintidós ADR", spec, StringComparison.Ordinal);
    }

    [Fact]
    public void Design_ClosesEveryVerificationFromV1ToV18_AndKeepsThePendingOnesAsPendingOfTheOperator()
    {
        var design = ReadRepositoryFile("openspec/changes/multi-compania/design.md");
        var lines = Section(design, "### Cierre de V-1 a V-18 (PR 8)", "\n### ").Split('\n');

        for (var number = 1; number <= 18; number++)
        {
            Assert.Single(lines, line => line.StartsWith($"| V-{number} |", StringComparison.Ordinal));
        }

        foreach (var pending in new[] { 1, 3, 4, 6, 7 })
        {
            Assert.Contains("Pendiente del responsable", lines.Single(line => line.StartsWith($"| V-{pending} |", StringComparison.Ordinal)), StringComparison.Ordinal);
        }

        // La columna «Estado» (segunda) de las verificaciones hechas no puede decir que esperan al responsable.
        foreach (var done in new[] { 2, 5, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 })
        {
            var cells = lines.Single(line => line.StartsWith($"| V-{done} |", StringComparison.Ordinal)).Split('|');
            Assert.DoesNotContain("Pendiente", cells[2], StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("### DD-7.", "### DD-8.", "AssistantRuntimeRegistry")]
    [InlineData("### DD-16.", "### DD-17.", "IAssistantQuota.TryConsumeMinute")]
    [InlineData("### DD-21.", "### DD-22.", "AddWithinCapAsync")]
    [InlineData("### DD-23.", "### DD-24.", "Ai__RuntimeIdleMinutes")]
    public void Design_RecordsInTheDecisionItselfWhatApplyClosed(string start, string end, string expected)
    {
        var design = ReadRepositoryFile("openspec/changes/multi-compania/design.md");
        var decision = Section(design, start, end);

        Assert.Contains("Cierre en apply", decision, StringComparison.Ordinal);
        Assert.Contains(expected, decision, StringComparison.Ordinal);
    }

    [Fact]
    public void Design_ListsTheCurrentRequirementsThatTheArchiveMustWarnAbout()
    {
        var design = ReadRepositoryFile("openspec/changes/multi-compania/design.md");
        var section = Section(design, "### Requisitos vigentes que el informe de archivo debe advertir", "\n### ");

        string[] required =
        [
            "Sin clave foránea de fichajes a usuarios", "TimeEntry.Start", "registro público", "fila única", "toda la instancia",
            "Página Web de configuración", "Menú según el rol", "ADR y README", "veintidós",
        ];
        var missing = Missing(section, required);
        Assert.True(missing.Count == 0, $"La lista de advertencias del archivo no menciona: {string.Join(", ", missing)}");
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
