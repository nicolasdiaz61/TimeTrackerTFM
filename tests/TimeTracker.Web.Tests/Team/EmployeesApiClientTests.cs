using System.Net;
using System.Text.Json;

using TimeTracker.Web.Team;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Team;

public class EmployeesApiClientTests
{
    private static readonly Guid Ana = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    private const string CreatedJson = """
        {"id":"3f2c9a52-5f0a-4d1e-9c55-0d7a2d6f9b11","displayName":"Pablo Ejemplo","email":"pablo@example.test","role":"Employee","temporaryPassword":"ClaveTemporal2468Xy"}
        """;

    private static EmployeesApiClient ClientFor(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    [Fact]
    public async Task Create_PostsExactlyTheFourContractFields_WithNoCompanyIdentifier()
    {
        var handler = new StubHandler(_ => Responses.Json(HttpStatusCode.Created, CreatedJson));

        var result = await ClientFor(handler).CreateAsync(new CreateEmployeeRequestDto("Pablo Ejemplo", "pablo@example.test", "Employee", Ana));

        Assert.Null(result.Error);
        Assert.Equal("ClaveTemporal2468Xy", result.Value!.TemporaryPassword);
        Assert.Equal("Pablo Ejemplo", result.Value.DisplayName);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/employees", handler.Request.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(["displayName", "email", "managerId", "role"], body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("ompan", handler.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WithoutAManager_SendsANullManagerId()
    {
        var handler = new StubHandler(_ => Responses.Json(HttpStatusCode.Created, CreatedJson));

        await ClientFor(handler).CreateAsync(new CreateEmployeeRequestDto("Pablo", "pablo@example.test", "Employee", null));

        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("managerId").ValueKind);
    }

    [Fact]
    public async Task Create_ReadsTheFieldErrors_TheConflictDetailAndTheRetryAfter()
    {
        var invalid = await ClientFor(new StubHandler(_ => Responses.Validation("email", "El correo electrónico debe ser válido.")))
            .CreateAsync(new CreateEmployeeRequestDto("P", "x", "Employee", null));
        var conflict = await ClientFor(new StubHandler(_ => Responses.Problem(HttpStatusCode.Conflict, "Ya existe una persona con ese correo electrónico.")))
            .CreateAsync(new CreateEmployeeRequestDto("P", "x", "Employee", null));
        var limited = await ClientFor(new StubHandler(_ => Responses.WithRetryAfter(HttpStatusCode.TooManyRequests, 42)))
            .CreateAsync(new CreateEmployeeRequestDto("P", "x", "Employee", null));

        Assert.Equal(["El correo electrónico debe ser válido."], invalid.Error!.FieldErrors["email"]);
        Assert.Equal("Ya existe una persona con ese correo electrónico.", conflict.Error!.Message);
        Assert.Equal(42, limited.Error!.RetryAfterSeconds);
    }

    [Fact]
    public async Task Regenerate_PostsToTheMemberRoute_WithNoBodyNorCompanyIdentifier()
    {
        var handler = new StubHandler(_ => Responses.Json(HttpStatusCode.OK, """{"temporaryPassword":"NuevaTemporal1357Zq"}"""));

        var result = await ClientFor(handler).RegenerateTemporaryPasswordAsync(Ana);

        Assert.Null(result.Error);
        Assert.Equal("NuevaTemporal1357Zq", result.Value!.TemporaryPassword);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/employees/a1111111-1111-1111-1111-111111111111/temporary-password", handler.Request.RequestUri!.ToString());
        Assert.True(string.IsNullOrEmpty(handler.Body));
    }

    [Fact]
    public async Task Regenerate_ReadsTheConflictDetail_AndKeepsTheNotFoundWithoutDetail()
    {
        var conflict = await ClientFor(new StubHandler(_ => Responses.Problem(HttpStatusCode.Conflict, "La persona ya cambió su contraseña.")))
            .RegenerateTemporaryPasswordAsync(Ana);
        var notFound = await ClientFor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))
            .RegenerateTemporaryPasswordAsync(Ana);

        Assert.Equal("La persona ya cambió su contraseña.", conflict.Error!.Message);
        Assert.Equal(HttpStatusCode.NotFound, notFound.Error!.StatusCode);
    }
}
