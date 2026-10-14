using System.Text.Json;

namespace TimeTracker.ArchitectureTests;

public class OpenApiContractTests
{
    private const string TimeEntriesPath = "/api/time-entries";

    [Fact]
    public void ExportedDocument_ContainsHealthGet200()
    {
        using var document = LoadDocument();
        var health = document.RootElement.GetProperty("paths").GetProperty("/health");

        Assert.True(health.GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
    }

    [Fact]
    public void ExportedDocument_ContainsTimeEntryEndpoints()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");

        AssertResponses(paths, $"{TimeEntriesPath}/clock-in", "post", "201", "409");
        AssertResponses(paths, $"{TimeEntriesPath}/clock-out", "post", "200", "409");
        AssertResponses(paths, TimeEntriesPath, "get", "200");

        var response = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TimeEntryResponse").GetProperty("properties");
        foreach (var name in new[] { "clockInAt", "clockOutAt" })
        {
            Assert.Equal("date-time", response.GetProperty(name).GetProperty("format").GetString());
        }
    }

    [Fact]
    public void ExportedDocument_ContainsAuthEndpoints()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");

        AssertResponses(paths, "/api/auth/login", "post", "200", "400", "401");
        AssertResponses(paths, "/api/auth/me", "get", "200", "401");
        AssertResponses(paths, $"{TimeEntriesPath}/clock-in", "post", "401");
        AssertResponses(paths, $"{TimeEntriesPath}/clock-out", "post", "401");
        AssertResponses(paths, TimeEntriesPath, "get", "401");
    }

    [Fact]
    public void ExportedDocument_DoesNotExposePasswordHash()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs", "api", "openapi.json"));

        // La respuesta del login describe al usuario sin su hash (API3, asignación masiva).
        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportedDocument_DoesNotExposeEmployeeId()
    {
        using var document = LoadDocument();

        // La identidad sale del token (AGENTS.md §6): ni parámetros ni esquemas pueden mencionar al empleado.
        var names = CollectNames(document.RootElement).ToList();

        Assert.DoesNotContain(names, name => name.Equals("employeeId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportedDocument_ContainsOnlyTheTwoTeamGetOperations()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");
        const string MemberEntries = "/api/team/members/{memberId}/time-entries";

        var teamPaths = paths.EnumerateObject().Where(path => path.Name.StartsWith("/api/team", StringComparison.Ordinal)).ToList();

        Assert.Equal(["/api/team/members", MemberEntries], teamPaths.Select(path => path.Name).Order(StringComparer.Ordinal));
        Assert.All(teamPaths, path => Assert.Equal(["get"], path.Value.EnumerateObject().Select(operation => operation.Name)));
        var parameter = Assert.Single(paths.GetProperty(MemberEntries).GetProperty("get").GetProperty("parameters").EnumerateArray());
        Assert.Equal("memberId", parameter.GetProperty("name").GetString());
        Assert.Equal("path", parameter.GetProperty("in").GetString());
        Assert.Equal("uuid", parameter.GetProperty("schema").GetProperty("format").GetString());
    }

    [Fact]
    public void ExportedDocument_TeamOperationsDeclareTheirResponses()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");

        AssertResponses(paths, "/api/team/members", "get", "200", "401", "403");
        AssertResponses(paths, "/api/team/members/{memberId}/time-entries", "get", "200", "401", "403", "404");
    }

    [Fact]
    public void ExportedDocument_LoginDeclares200_400_401_429()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");

        AssertResponses(paths, "/api/auth/login", "post", "200", "400", "401", "429");
        AssertResponses(paths, "/api/auth/me", "get", "200", "401");
    }

    [Fact]
    public void ExportedDocument_DeclaresBearerScheme()
    {
        using var document = LoadDocument();
        var root = document.RootElement;

        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
        Assert.True(RequiresBearer(root, "/api/time-entries", "get"));
        Assert.True(RequiresBearer(root, "/api/team/members", "get"));
        Assert.False(RequiresBearer(root, "/api/auth/login", "post"));
        Assert.False(RequiresBearer(root, "/health", "get"));
    }

    // Un requisito de la operación sustituye al global; «security: []» en la operación lo deja sin requisitos.
    private static bool RequiresBearer(JsonElement root, string path, string method)
    {
        var operation = root.GetProperty("paths").GetProperty(path).GetProperty(method);
        var security = operation.TryGetProperty("security", out var own) ? own
            : root.TryGetProperty("security", out var global) ? global : default;

        return security.ValueKind == JsonValueKind.Array
            && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _));
    }

    private static JsonDocument LoadDocument()
    {
        var path = Path.Combine(RepositoryRoot.Path, "docs", "api", "openapi.json");

        // El documento lo genera el build de Api; si falta, hay que compilar la solución.
        Assert.True(File.Exists(path), $"No existe {path}. Compile TimeTracker.Api para generarlo.");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static void AssertResponses(JsonElement paths, string path, string method, params string[] statusCodes)
    {
        Assert.True(paths.TryGetProperty(path, out var item), $"Falta la ruta {path} en el contrato.");
        Assert.True(item.TryGetProperty(method, out var operation), $"Falta {method.ToUpperInvariant()} {path}.");
        var responses = operation.GetProperty("responses");
        foreach (var statusCode in statusCodes)
        {
            Assert.True(responses.TryGetProperty(statusCode, out _), $"{method.ToUpperInvariant()} {path} no declara {statusCode}.");
        }
    }

    // Nombres de parámetros y de propiedades de esquema de todo el documento.
    private static IEnumerable<string> CollectNames(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == "properties" && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var schemaProperty in property.Value.EnumerateObject())
                        {
                            yield return schemaProperty.Name;
                        }
                    }

                    if (property.Name == "parameters" && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var parameter in property.Value.EnumerateArray())
                        {
                            if (parameter.TryGetProperty("name", out var name) && name.GetString() is { } text)
                            {
                                yield return text;
                            }
                        }
                    }

                    foreach (var nested in CollectNames(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray().SelectMany(CollectNames))
                {
                    yield return item;
                }

                break;
        }
    }
}
