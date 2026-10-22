using System.Buffers.Text;
using System.Text.Json.Nodes;

namespace TimeTracker.Api.IntegrationTests;

public class TestTokensTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ByDefault_CarriesTheDefaultCompanyClaimsAndNoPasswordChangeMarker()
    {
        var payload = PayloadOf(TestTokens.Create(Guid.NewGuid(), "Employee", Now));

        Assert.Equal(TestTokens.DefaultCompanyId, (string?)payload["cid"]);
        Assert.Equal(TestTokens.DefaultCompanyName, (string?)payload["cname"]);
        Assert.False(payload.ContainsKey("mcp"));
    }

    [Fact]
    public void Create_WithExplicitCompanyValues_WritesThemAsGiven()
    {
        var companyId = Guid.NewGuid().ToString();

        var payload = PayloadOf(TestTokens.Create(Guid.NewGuid(), "Manager", Now, companyId: companyId, companyName: "Acme Norte", passwordChangePending: "true"));

        Assert.Equal(companyId, (string?)payload["cid"]);
        Assert.Equal("Acme Norte", (string?)payload["cname"]);
        Assert.Equal("true", (string?)payload["mcp"]);
    }

    [Fact]
    public void Create_WithNullCompanyValues_OmitsTheClaims()
    {
        var payload = PayloadOf(TestTokens.Create(Guid.NewGuid(), "Employee", Now, companyId: null, companyName: null));

        Assert.False(payload.ContainsKey("cid"));
        Assert.False(payload.ContainsKey("cname"));
    }

    [Fact]
    public void CreateUnsigned_CarriesTheDefaultCompanyClaims()
    {
        var payload = PayloadOf(TestTokens.CreateUnsigned(Guid.NewGuid(), "Employee", Now));

        Assert.Equal(TestTokens.DefaultCompanyId, (string?)payload["cid"]);
    }

    private static JsonObject PayloadOf(string token)
        => JsonNode.Parse(Base64Url.DecodeFromChars(token.Split('.')[1]))!.AsObject();
}
