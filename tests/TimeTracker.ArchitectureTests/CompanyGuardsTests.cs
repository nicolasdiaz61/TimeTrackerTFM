using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// TC-7 (DD-9, spec solution-architecture «Contrato sin companyId» y «Sin parámetros de compañía en el código»): la compañía sale
/// solo del token. Ningún contrato, petición, comando, caso de uso, herramienta del asistente ni endpoint la recibe del cliente, y
/// los repositorios no la reciben como parámetro de consulta.
/// </summary>
public partial class CompanyGuardsTests
{
    private const string CompanyIdName = "companyid";

    // Los únicos archivos que pueden contener el literal de cada claim (el resto usa las constantes de JwtDefaults).
    private static readonly string[] CidLiteralFiles =
    [
        "src/TimeTracker.Api/Authentication/JwtDefaults.cs",
        "src/TimeTracker.Api/Authentication/JwtAccessTokenIssuer.cs",
        "src/TimeTracker.Api/Authentication/AuthenticationServiceCollectionExtensions.cs",
        "src/TimeTracker.Api/Authentication/HttpContextCurrentUser.cs",
        "src/TimeTracker.Api/Security/CompanyContextMiddleware.cs",
    ];

    private static readonly string[] CnameLiteralFiles =
    [
        "src/TimeTracker.Api/Authentication/JwtDefaults.cs",
        "src/TimeTracker.Api/Authentication/JwtAccessTokenIssuer.cs",
        "src/TimeTracker.Api/Authentication/AuthEndpoints.cs",
        // La Web lee «cname» solo para mostrar el nombre de la compañía (WS «Estado de autenticación por claims»); nunca «cid».
        "src/TimeTracker.Web/Auth/SessionClaims.cs",
    ];

    // Repositorio o almacén -> método permitido con un parámetro Guid de compañía. `FindLoginInfoAsync` lee `companies` por clave primaria.
    private static readonly string[] CompanyParameterExceptions =
    [
        "ICompanyRepository.FindLoginInfoAsync",
        "CompanyRepository.FindLoginInfoAsync",
    ];

    [Fact]
    public void OpenApiDocument_DeclaresNoCompanyIdParameterNorProperty()
    {
        var path = Path.Combine(RepositoryRoot.Path, "docs", "api", "openapi.json");
        Assert.True(File.Exists(path), $"No existe {path}. Compile TimeTracker.Api para generarlo.");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var offenders = CompanyIdNames(document.RootElement);

        Assert.True(offenders.Count == 0, $"El contrato OpenAPI declara companyId: {string.Join(", ", offenders)}");
        Assert.NotEmpty(AllNames(document.RootElement));
    }

    [Fact]
    public void OpenApiScanner_FlagsCompanyIdInAnyCaseButNotCompanyName()
    {
        using var parameter = JsonDocument.Parse("""{"paths":{"/x":{"get":{"parameters":[{"name":"CompanyId","in":"query"},{"name":"from","in":"query"}]}}}}""");
        using var property = JsonDocument.Parse("""{"components":{"schemas":{"A":{"properties":{"companyId":{},"companyName":{}}}}}}""");

        Assert.Equal(["CompanyId"], CompanyIdNames(parameter.RootElement));
        Assert.Equal(["companyId"], CompanyIdNames(property.RootElement));
    }

    [Fact]
    public void RequestsCommandsUseCasesToolsAndEndpoints_DeclareNoCompanyId()
    {
        var types = ClientFacingTypes().ToList();
        Assert.Contains(types, type => type.Name == "ClockInUseCase");
        Assert.Contains(types, type => type.Name == "AssistantToolset");
        Assert.Contains(types, type => type.Name == "TimeEntryEndpoints");
        Assert.Contains(types, type => type.Name.EndsWith("Request", StringComparison.Ordinal));

        var offenders = CompanyIdDeclarations(types);

        Assert.True(offenders.Count == 0, $"Firmas con companyId en tipos que reciben datos del cliente (DD-9): {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Checker_FlagsCompanyIdInAPropertyAMethodParameterAndAConstructorParameter()
    {
        var offenders = CompanyIdDeclarations([typeof(SampleRequest), typeof(SampleUseCase), typeof(SampleClean)]);

        Assert.Equal(
            ["SampleRequest.CompanyId", "SampleUseCase.Execute(companyId)", "SampleUseCase..ctor(CompanyID)"],
            offenders);
    }

    [Fact]
    public void RepositoriesAndStores_DoNotTakeAGuidCompanyIdAsAQueryParameter()
    {
        var types = SourceAssemblies().SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Repository", StringComparison.Ordinal) || type.Name.EndsWith("Store", StringComparison.Ordinal))
            .ToList();
        Assert.Contains(types, type => type.Name == "UserRepository");
        Assert.Contains(types, type => type.Name == "AssistantSettingsStore");

        var offenders = types
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetParameters().Any(IsCompanyGuid))
                .Select(method => $"{type.Name}.{method.Name}"))
            .Except(CompanyParameterExceptions)
            .ToList();

        Assert.True(offenders.Count == 0, $"Repositorios o almacenes con Guid companyId como parámetro (DD-9): {string.Join(", ", offenders)}");
        Assert.Contains(types.SelectMany(type => type.GetMethods()), method => method.Name == "FindLoginInfoAsync");
    }

    [Fact]
    public void TheCidAndCnameLiterals_LiveOnlyInTheFilesThatIssueOrValidateThem()
    {
        var misplaced = SourceFiles()
            .SelectMany(file => LiteralsOutsideAllowedFiles(file))
            .ToList();

        Assert.True(misplaced.Count == 0, $"Literal de claim fuera de los archivos permitidos (usa JwtDefaults): {string.Join(", ", misplaced)}");
    }

    [Theory]
    [InlineData("var claim = \"cid\";", true)]
    [InlineData("claim.Type == \"cid\"", true)]
    [InlineData("// el claim \"cid\" del token", false)]
    [InlineData("var text = \"acid rain\";", false)]
    [InlineData("var claim = JwtDefaults.CompanyClaim;", false)]
    public void LiteralScanner_DetectsTheCidStringLiteralInCodeOnly(string source, bool expected)
        => Assert.Equal(expected, CidPattern().IsMatch(StripComments(source)));

    private static IEnumerable<string> LiteralsOutsideAllowedFiles(string file)
    {
        var relative = Path.GetRelativePath(RepositoryRoot.Path, file).Replace('\\', '/');
        var code = StripComments(File.ReadAllText(file));
        if (CidPattern().IsMatch(code) && !CidLiteralFiles.Contains(relative))
        {
            yield return $"{relative} (\"cid\")";
        }

        if (CnamePattern().IsMatch(code) && !CnameLiteralFiles.Contains(relative))
        {
            yield return $"{relative} (\"cname\")";
        }
    }

    // Tipos que reciben datos del cliente o de un modelo: peticiones del Api, comandos y casos de uso de Application, herramientas
    // del asistente y endpoints.
    private static IEnumerable<Type> ClientFacingTypes()
    {
        var application = Assembly.Load("TimeTracker.Application").GetTypes();
        var api = Assembly.Load("TimeTracker.Api").GetTypes();
        var tools = Assembly.Load("TimeTracker.Infrastructure").GetTypes().Where(type => type.Name.Contains("Toolset", StringComparison.Ordinal));
        return application.Where(type => type.Name.EndsWith("UseCase", StringComparison.Ordinal) || type.Name.EndsWith("Command", StringComparison.Ordinal))
            .Concat(api.Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Endpoints", StringComparison.Ordinal)))
            .Concat(tools);
    }

    private static List<string> CompanyIdDeclarations(IEnumerable<Type> types)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();
        foreach (var type in types)
        {
            offenders.AddRange(type.GetProperties(all).Where(property => IsCompanyName(property.Name)).Select(property => $"{type.Name}.{property.Name}"));
            offenders.AddRange(type.GetMethods(all).SelectMany(method => method.GetParameters().Where(parameter => IsCompanyName(parameter.Name)).Select(parameter => $"{type.Name}.{method.Name}({parameter.Name})")));
            offenders.AddRange(type.GetConstructors(all).SelectMany(constructor => constructor.GetParameters().Where(parameter => IsCompanyName(parameter.Name)).Select(parameter => $"{type.Name}..ctor({parameter.Name})")));
        }

        return offenders.Distinct().ToList();
    }

    private static bool IsCompanyGuid(ParameterInfo parameter)
        => (parameter.ParameterType == typeof(Guid) || parameter.ParameterType == typeof(Guid?)) && IsCompanyName(parameter.Name);

    private static bool IsCompanyName(string? name) => string.Equals(name, CompanyIdName, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<Assembly> SourceAssemblies()
        => new[] { "TimeTracker.Application", "TimeTracker.Infrastructure", "TimeTracker.Api" }.Select(Assembly.Load);

    // Nombres de parámetro (de ruta, consulta o cabecera) y de propiedad de esquema del documento, en cualquier nivel.
    private static List<string> CompanyIdNames(JsonElement root) => AllNames(root).Where(IsCompanyName).ToList();

    private static List<string> AllNames(JsonElement element)
    {
        var names = new List<string>();
        Walk(element, names);
        return names;
    }

    private static void Walk(JsonElement element, List<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && element.TryGetProperty("in", out _))
                {
                    names.Add(name.GetString()!);
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == "properties" && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        names.AddRange(property.Value.EnumerateObject().Select(child => child.Name));
                    }

                    Walk(property.Value, names);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, names);
                }

                break;
            default:
                break;
        }
    }

    private static IEnumerable<string> SourceFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal));
    }

    private static string StripComments(string source)
        => string.Join('\n', source.Split('\n').Select(line =>
        {
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment < 0 ? line : line[..comment];
        }));

    [GeneratedRegex("\"cid\"")]
    private static partial Regex CidPattern();

    [GeneratedRegex("\"cname\"")]
    private static partial Regex CnamePattern();

    private sealed class SampleRequest
    {
        public Guid CompanyId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class SampleUseCase(Guid CompanyID)
    {
        public Guid Seen { get; } = CompanyID;

        public void Execute(Guid companyId) => _ = companyId;
    }

    private sealed class SampleClean
    {
        public string CompanyName { get; set; } = string.Empty;

        public void Execute(Guid userId) => _ = userId;
    }
}
