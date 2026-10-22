using System.Net;
using System.Text.Json;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Registration;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Registration;

public class RegisterCompanyClientTests
{
    private const string LoginJson = """
        {"accessToken":"token-ficticio","expiresAt":"2026-10-22T10:30:00Z",
         "user":{"id":"3f2c9a52-5f0a-4d1e-9c55-0d7a2d6f9b11","email":"ana@example.test","displayName":"Ana Ejemplo","role":"Manager","companyName":"Compañía Ejemplo","mustChangePassword":false}}
        """;

    private static AuthApiClient ClientFor(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    private static RegisterCompanyRequestDto Request() => new("C", "A", "a@example.test", "x", "x");

    [Fact]
    public async Task RegisterCompany_PostsExactlyTheFiveContractFields_WithoutAuthorization()
    {
        var handler = new StubHandler(_ => Responses.Json(HttpStatusCode.Created, LoginJson));

        var result = await ClientFor(handler).RegisterCompanyAsync(new RegisterCompanyRequestDto("Compañía", "Ana", "ana@example.test", "contrasena-prueba-12345", "codigo-prueba"));

        Assert.Null(result.Error);
        Assert.Equal("token-ficticio", result.Value!.AccessToken);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/auth/register-company", handler.Request.RequestUri!.ToString());
        Assert.Null(handler.Request.Headers.Authorization);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(
            ["companyName", "email", "inviteCode", "managerName", "password"],
            body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task RegisterCompany_ReadsTheFieldErrorsOfABadRequest()
    {
        var handler = new StubHandler(_ => Responses.Validation("password", "La contraseña debe tener al menos 12 caracteres."));

        var result = await ClientFor(handler).RegisterCompanyAsync(Request());

        Assert.Equal(HttpStatusCode.BadRequest, result.Error!.StatusCode);
        Assert.Equal(["La contraseña debe tener al menos 12 caracteres."], result.Error.FieldErrors["password"]);
    }

    [Fact]
    public async Task RegisterCompany_KeepsTheDetailOnlyOfAConflict_AndNeverOfAForbidden()
    {
        var conflict = await ClientFor(new StubHandler(_ => Responses.Problem(HttpStatusCode.Conflict, "Ya existe una compañía con ese nombre.")))
            .RegisterCompanyAsync(Request());
        var forbidden = await ClientFor(new StubHandler(_ => Responses.Problem(HttpStatusCode.Forbidden, "Detalle que no debe verse.")))
            .RegisterCompanyAsync(Request());

        Assert.Equal("Ya existe una compañía con ese nombre.", conflict.Error!.Message);
        Assert.DoesNotContain("Detalle", forbidden.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterCompany_ReadsTheRetryAfterOfATooManyRequests()
    {
        var handler = new StubHandler(_ => Responses.WithRetryAfter(HttpStatusCode.TooManyRequests, 120));

        var result = await ClientFor(handler).RegisterCompanyAsync(Request());

        Assert.Equal(120, result.Error!.RetryAfterSeconds);
    }

    [Fact]
    public async Task RegisterCompany_WithTheNetworkDown_ReturnsAFailureWithoutStatus()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("sin red"));

        var result = await ClientFor(handler).RegisterCompanyAsync(Request());

        Assert.Null(result.Error!.StatusCode);
        Assert.DoesNotContain("sin red", result.Error.Message, StringComparison.Ordinal);
    }
}
