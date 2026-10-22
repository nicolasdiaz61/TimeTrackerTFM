using System.Text.Json;

namespace TimeTracker.ArchitectureTests;

public class OpenApiContractTests
{
    private const string TimeEntriesPath = "/api/time-entries";
    private const string ConfigurationPath = "/api/asistente/configuracion";
    private const string RegisterCompanyPath = "/api/auth/register-company";

    // Código con el que DocumentGeneration abre el registro solo para exportar el contrato (Api/Configuration/DocumentGeneration.cs).
    private const string FictitiousGenerationCode = "codigo-ficticio-solo-para-exportar-openapi";

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
        foreach (var action in new[] { "start-break", "start-meal-break", "end-break" })
        {
            AssertResponses(paths, $"{TimeEntriesPath}/{action}", "post", "200", "401", "409");
        }

        AssertResponses(paths, TimeEntriesPath, "get", "200", "400", "401");
        var windowParameters = paths.GetProperty(TimeEntriesPath).GetProperty("get").GetProperty("parameters").EnumerateArray().ToList();
        Assert.Equal(["from", "to"], windowParameters.Select(parameter => parameter.GetProperty("name").GetString()));
        Assert.All(windowParameters, parameter => Assert.Equal("query", parameter.GetProperty("in").GetString()));

        var response = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TimeEntryResponse").GetProperty("properties");
        Assert.False(response.TryGetProperty("workedMinutes", out _));
        Assert.True(response.TryGetProperty("durationMinutes", out _));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal(["Work", "Break", "MealBreak"], schemas.GetProperty("TimeEntryType").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["Idle", "Working", "OnBreak", "OnMealBreak"], schemas.GetProperty("TimeTrackingStatus").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        var list = schemas.GetProperty("TimeEntryListResponse").GetProperty("properties");
        foreach (var name in new[] { "totalWorkedMinutes", "totalBreakMinutes", "totalMealBreakMinutes", "status" })
        {
            Assert.True(list.TryGetProperty(name, out _), $"TimeEntryListResponse no declara {name}.");
        }

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
        Assert.True(RequiresBearer(document.RootElement, $"{TimeEntriesPath}/start-break", "post"));
        Assert.True(RequiresBearer(document.RootElement, $"{TimeEntriesPath}/start-meal-break", "post"));
        Assert.True(RequiresBearer(document.RootElement, $"{TimeEntriesPath}/end-break", "post"));
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
        var parameters = paths.GetProperty(MemberEntries).GetProperty("get").GetProperty("parameters").EnumerateArray().ToList();
        Assert.Equal(["memberId", "from", "to"], parameters.Select(parameter => parameter.GetProperty("name").GetString()));
        Assert.Equal(["path", "query", "query"], parameters.Select(parameter => parameter.GetProperty("in").GetString()));
        Assert.Equal("uuid", parameters[0].GetProperty("schema").GetProperty("format").GetString());
    }

    [Fact]
    public void ExportedDocument_TeamOperationsDeclareTheirResponses()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");

        AssertResponses(paths, "/api/team/members", "get", "200", "401", "403");
        AssertResponses(paths, "/api/team/members/{memberId}/time-entries", "get", "200", "400", "401", "403", "404");
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

    [Fact]
    public void ExportedDocument_ContainsOnlyThePostAssistantOperationWithItsResponses()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");

        var assistant = paths.GetProperty("/api/asistente");
        Assert.Equal(["post"], assistant.EnumerateObject().Select(operation => operation.Name));
        AssertResponses(paths, "/api/asistente", "post", "200", "400", "401", "429", "503");
        // El 413 por cuerpo grande lo rechaza el servidor antes de enlazarlo y no se declara.
        Assert.False(assistant.GetProperty("post").GetProperty("responses").TryGetProperty("413", out _));
        Assert.True(RequiresBearer(document.RootElement, "/api/asistente", "post"));

        var operationText = assistant.GetRawText();
        Assert.DoesNotContain("employeeId", operationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("memberId", operationText, StringComparison.OrdinalIgnoreCase);
        Assert.False(assistant.GetProperty("post").TryGetProperty("parameters", out _));

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal(["message", "history"], schemas.GetProperty("AssistantRequest").GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.Equal(["role", "text"], schemas.GetProperty("AssistantTurnRequest").GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.Equal(["reply"], schemas.GetProperty("AssistantResponse").GetProperty("properties").EnumerateObject().Select(property => property.Name));

        // «history» es opcional (ausente equivale a vacío); mensaje, rol y texto sí son obligatorios.
        Assert.Equal(["message"], schemas.GetProperty("AssistantRequest").GetProperty("required").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(["role", "text"], schemas.GetProperty("AssistantTurnRequest").GetProperty("required").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    public void ExportedDocument_AssistantConfigurationRoutesHaveExactlyTheOperationsAndResponsesOfDd14()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty(ConfigurationPath, out var configuration), $"Falta la ruta {ConfigurationPath} en el contrato.");
        Assert.Equal(["delete", "get", "put"], configuration.EnumerateObject().Select(operation => operation.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["post"], paths.GetProperty($"{ConfigurationPath}/prueba").EnumerateObject().Select(operation => operation.Name));
        Assert.Equal(["200", "401", "403"], ResponseCodes(paths, ConfigurationPath, "get"));
        Assert.Equal(["200", "400", "401", "403", "409"], ResponseCodes(paths, ConfigurationPath, "put"));
        Assert.Equal(["204", "401", "403", "409"], ResponseCodes(paths, ConfigurationPath, "delete"));
        Assert.Equal(["200", "400", "401", "403", "429"], ResponseCodes(paths, $"{ConfigurationPath}/prueba", "post"));
        Assert.True(RequiresBearer(document.RootElement, ConfigurationPath, "get"));
        Assert.True(RequiresBearer(document.RootElement, ConfigurationPath, "put"));
        Assert.True(RequiresBearer(document.RootElement, ConfigurationPath, "delete"));
        Assert.True(RequiresBearer(document.RootElement, $"{ConfigurationPath}/prueba", "post"));
        // El 413 por cuerpo grande lo rechaza el servidor antes de enlazarlo y no se declara.
        Assert.DoesNotContain("413", ResponseCodes(paths, ConfigurationPath, "put"));
        // El borrado no lleva token ni cuerpo: el servidor captura la versión que lee (DD-8, DD-14).
        Assert.False(configuration.GetProperty("delete").TryGetProperty("requestBody", out _));
        Assert.False(configuration.GetProperty("delete").TryGetProperty("parameters", out _));
    }

    [Fact]
    public void ExportedDocument_ConfigurationSchemasDeclareExactlyTheDd14Fields()
    {
        using var document = LoadDocument();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        Assert.Equal(
            ["concurrencyToken", "credential", "effort", "maxOutputTokens", "model", "provider", "timeoutSeconds"],
            PropertyNames(schemas, "AssistantConfigurationRequest").Order(StringComparer.Ordinal));
        Assert.Equal(
            ["credential", "effort", "maxOutputTokens", "model", "provider", "timeoutSeconds"],
            PropertyNames(schemas, "AssistantConnectionTestRequest").Order(StringComparer.Ordinal));
        Assert.Equal(
            ["allowedProviders", "applied", "concurrencyToken", "credentialConfigured", "credentialStorageAvailable", "effort", "maxOutputTokens", "model", "provider", "sources", "timeoutSeconds", "updatedAt", "updatedBy"],
            PropertyNames(schemas, "AssistantConfigurationResponse").Order(StringComparer.Ordinal));
        Assert.Equal(["message", "result"], PropertyNames(schemas, "AssistantConnectionTestResponse").Order(StringComparer.Ordinal));
        // Las peticiones rechazan propiedades desconocidas (E-7) y el contrato lo declara.
        Assert.False(schemas.GetProperty("AssistantConfigurationRequest").GetProperty("additionalProperties").GetBoolean());
        Assert.False(schemas.GetProperty("AssistantConnectionTestRequest").GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void ExportedDocument_ConfigurationSchemasHaveNoUrlOrEndpointProperty()
    {
        using var document = LoadDocument();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var configurationSchemas = schemas.EnumerateObject().Where(schema => schema.Name.StartsWith("Assistant", StringComparison.Ordinal) || schema.Name == "AllowedProviderResponse").ToList();

        // Sin campo de URL no hay forma de dirigir la clave a otro destino (SSRF, DD-3).
        Assert.Contains(configurationSchemas, schema => schema.Name == "AssistantConfigurationRequest");
        var names = configurationSchemas.SelectMany(schema => PropertyNames(schemas, schema.Name)).ToList();
        Assert.NotEmpty(names);
        Assert.DoesNotContain(names, name => name.Contains("url", StringComparison.OrdinalIgnoreCase) || name.Contains("endpoint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportedDocument_TheKeyIsInRequestsAndOnlyItsIndicatorInResponses()
    {
        using var document = LoadDocument();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        Assert.Contains("credential", PropertyNames(schemas, "AssistantConfigurationRequest"));
        Assert.Contains("credential", PropertyNames(schemas, "AssistantConnectionTestRequest"));
        Assert.Contains("credentialConfigured", PropertyNames(schemas, "AssistantConfigurationResponse"));
        foreach (var response in new[] { "AssistantConfigurationResponse", "AssistantConnectionTestResponse", "AllowedProviderResponse" })
        {
            Assert.DoesNotContain("credential", PropertyNames(schemas, response));
        }

        // Ningún otro esquema de respuesta declara la clave. «credential» dentro de «sources» es solo el origen del valor (env, stored, none).
        var withKey = schemas.EnumerateObject().Where(schema => PropertyNames(schemas, schema.Name).Contains("credential")).Select(schema => schema.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["AssistantConfigurationRequest", "AssistantConfigurationSources", "AssistantConnectionTestRequest"], withKey);
        Assert.Equal("string", schemas.GetProperty("AssistantConfigurationSources").GetProperty("properties").GetProperty("credential").GetProperty("type").GetString());
    }

    [Fact]
    public void ExportedDocument_ContainsOnlyThePostRegisterCompanyOperation_AnonymousWithItsResponses()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths");

        var register = paths.GetProperty(RegisterCompanyPath);
        Assert.Equal(["post"], register.EnumerateObject().Select(operation => operation.Name));
        Assert.Equal(["201", "400", "403", "409", "429"], ResponseCodes(paths, RegisterCompanyPath, "post"));
        // Anónima: «security: []» en la operación retira el requisito Bearer global.
        Assert.False(RequiresBearer(document.RootElement, RegisterCompanyPath, "post"));
        Assert.Equal(0, register.GetProperty("post").GetProperty("security").GetArrayLength());
        // El 413 por cuerpo grande lo rechaza el servidor antes de enlazarlo y no se declara.
        Assert.DoesNotContain("413", ResponseCodes(paths, RegisterCompanyPath, "post"));
    }

    [Fact]
    public void ExportedDocument_RegisterCompanyRequestHasExactlyTheFivePropertiesAndNoCompanyId()
    {
        using var document = LoadDocument();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        Assert.Equal(
            ["companyName", "email", "inviteCode", "managerName", "password"],
            PropertyNames(schemas, "RegisterCompanyRequest").Order(StringComparer.Ordinal));
        var reference = document.RootElement.GetProperty("paths").GetProperty(RegisterCompanyPath).GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("oneOf").EnumerateArray()
            .Select(option => option.TryGetProperty("$ref", out var value) ? value.GetString() : null).Single(value => value is not null);
        Assert.EndsWith("/RegisterCompanyRequest", reference, StringComparison.Ordinal);
        // El cuerpo ignora las propiedades desconocidas (no rechaza): el contrato no declara additionalProperties falso.
        Assert.False(schemas.GetProperty("RegisterCompanyRequest").TryGetProperty("additionalProperties", out _));
    }

    [Fact]
    public void ExportedDocument_RegisterCompanyHasNoInviteCodeExampleAndNoFictitiousGenerationCode()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs", "api", "openapi.json"));

        Assert.DoesNotContain(FictitiousGenerationCode, text, StringComparison.Ordinal);
        Assert.DoesNotContain("codigo-prueba", text, StringComparison.Ordinal);
        using var document = LoadDocument();
        var inviteCode = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("RegisterCompanyRequest").GetProperty("properties").GetProperty("inviteCode");
        Assert.False(inviteCode.TryGetProperty("example", out _));
        Assert.False(inviteCode.TryGetProperty("default", out _));
        Assert.False(inviteCode.TryGetProperty("enum", out _));
    }

    // Tarea 5.7: esta entrega añade solo el registro; el alta, la regeneración y el cambio de contraseña entran en la PR 6 (tarea 6.14,
    // que sustituye esta comprobación por las de sus propias operaciones).
    [Fact]
    public void ExportedDocument_DoesNotYetContainTheOperationsOfThePasswordAndEmployeesEntrega()
    {
        using var document = LoadDocument();
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();

        Assert.DoesNotContain("/api/auth/change-password", paths);
        Assert.DoesNotContain(paths, path => path.StartsWith("/api/employees", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportedDocument_NeverMentionsTheProviderApiKey()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs", "api", "openapi.json"));

        Assert.DoesNotContain("apikey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ai__", text, StringComparison.Ordinal);
    }

    private static IEnumerable<string> ResponseCodes(JsonElement paths, string path, string method)
        => paths.GetProperty(path).GetProperty(method).GetProperty("responses").EnumerateObject().Select(response => response.Name).Order(StringComparer.Ordinal);

    private static List<string> PropertyNames(JsonElement schemas, string schema)
        => schemas.GetProperty(schema).TryGetProperty("properties", out var properties)
            ? [.. properties.EnumerateObject().Select(property => property.Name)]
            : [];

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
