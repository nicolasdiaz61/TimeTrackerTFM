using System.Net;
using System.Text.Json;

using TimeTracker.Web.Account;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Account;

public class AccountApiClientTests
{
    private const string LoginJson = """
        {"accessToken":"token-nuevo","expiresAt":"2026-10-22T10:30:00Z",
         "user":{"id":"3f2c9a52-5f0a-4d1e-9c55-0d7a2d6f9b11","email":"luis@example.test","displayName":"Luis Ejemplo","role":"Employee","companyName":"Compañía Ejemplo","mustChangePassword":false}}
        """;

    private static AccountApiClient ClientFor(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    [Fact]
    public async Task ChangePassword_PostsOnlyTheTwoContractFields_AndReadsTheNewSession()
    {
        var handler = new StubHandler(_ => Responses.Json(HttpStatusCode.OK, LoginJson));

        var result = await ClientFor(handler).ChangePasswordAsync(new ChangePasswordRequestDto("actual-prueba-1", "nueva-prueba-12345"));

        Assert.Null(result.Error);
        Assert.Equal("token-nuevo", result.Value!.AccessToken);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/auth/change-password", handler.Request.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(["currentPassword", "newPassword"], body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ChangePassword_ReadsTheFieldErrorsOfABadRequest()
    {
        var handler = new StubHandler(_ => Responses.Validation("currentPassword", "La contraseña actual no es correcta."));

        var result = await ClientFor(handler).ChangePasswordAsync(new ChangePasswordRequestDto("x", "y"));

        Assert.Equal(HttpStatusCode.BadRequest, result.Error!.StatusCode);
        Assert.Equal(["La contraseña actual no es correcta."], result.Error.FieldErrors["currentPassword"]);
    }

    [Fact]
    public async Task ChangePassword_ReadsTheRetryAfterAndTheConflictDetail()
    {
        var limited = await ClientFor(new StubHandler(_ => Responses.WithRetryAfter(HttpStatusCode.TooManyRequests, 30)))
            .ChangePasswordAsync(new ChangePasswordRequestDto("x", "y"));
        var conflict = await ClientFor(new StubHandler(_ => Responses.Problem(HttpStatusCode.Conflict, "Vuelva a intentarlo.")))
            .ChangePasswordAsync(new ChangePasswordRequestDto("x", "y"));

        Assert.Equal(30, limited.Error!.RetryAfterSeconds);
        Assert.Equal("Vuelva a intentarlo.", conflict.Error!.Message);
    }
}
