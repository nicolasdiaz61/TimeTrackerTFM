using System.Net;
using System.Net.Http.Headers;

namespace TimeTracker.Api.IntegrationTests.Authentication;

/// <summary>
/// Validación estricta del claim <c>cid</c> (AU «Compañía en el token…»). Cada caso se contrasta con un control válido
/// (<c>/api/auth/me</c> responde 200) que difiere en una sola cosa. No necesita PostgreSQL: la validación ocurre antes de
/// cualquier consulta.
/// </summary>
public class CompanyClaimTests
{
    private static readonly Guid UserId = Guid.Parse("0e6b1b7e-2f0b-4c1e-9d5f-6a1c3d9b7e21");
    private static readonly string OtherCompanyId = Guid.Parse("f3c5a0de-5b6e-4e77-8a3b-2d8c9b1e4a77").ToString();

    [Fact]
    public async Task Token_WithAValidCid_Returns200()
    {
        using var factory = new ApiFactory();

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, Token(factory)));
    }

    [Fact]
    public async Task Token_WithoutCid_Returns401OnMeAndOnTimeEntries()
    {
        using var factory = new ApiFactory();
        var token = Token(factory, companyId: null);

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, token, "/api/time-entries"));
    }

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public async Task Token_WithAnInvalidCid_Returns401(string companyId)
    {
        using var factory = new ApiFactory();

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, Token(factory, companyId: companyId), "/api/time-entries"));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, Token(factory, companyId: companyId)));
    }

    [Fact]
    public async Task Token_WithThePayloadAlteredToAnotherCompany_Returns401()
    {
        using var factory = new ApiFactory();
        var original = Token(factory);

        var altered = TestTokens.ReplacePayload(original, payload => payload["cid"] = OtherCompanyId);

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, original));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, altered, "/api/time-entries"));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Token_WithAnMcpThatIsNotTrue_IsAcceptedAsNoPendingChange(string passwordChangePending)
    {
        using var factory = new ApiFactory();

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, Token(factory, passwordChangePending: passwordChangePending)));
    }

    [Fact]
    public async Task Token_WithACidButWithoutSubOrRole_Returns401()
    {
        using var factory = new ApiFactory();
        var withoutSub = TestTokens.ReplacePayload(Token(factory), payload => payload.Remove("sub"));
        var withoutRole = TestTokens.ReplacePayload(Token(factory), payload => payload.Remove("role"));

        // Alterar el payload invalida la firma: se fabrican firmados de nuevo con un sub y un rol inválidos.
        var invalidSub = TestTokens.Create(UserId, "Employee", factory.Clock.GetUtcNow(), subject: "no-es-un-guid");
        var invalidRole = TestTokens.Create(UserId, "Admin", factory.Clock.GetUtcNow());

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, withoutSub));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, withoutRole));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, invalidSub));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(factory, invalidRole));
    }

    private static string Token(ApiFactory factory, string? companyId = TestTokens.DefaultCompanyId, string? passwordChangePending = null)
        => TestTokens.Create(UserId, "Employee", factory.Clock.GetUtcNow(), companyId: companyId, passwordChangePending: passwordChangePending);

    private static async Task<HttpStatusCode> StatusAsync(ApiFactory factory, string token, string path = "/api/auth/me")
    {
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync(path, CancellationToken.None);
        return response.StatusCode;
    }
}
