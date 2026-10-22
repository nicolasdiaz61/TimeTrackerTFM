using TimeTracker.Web.Assistant;

namespace TimeTracker.Web.Tests.Assistant;

public class AssistantConfigurationNoticeTests
{
    [Fact]
    public void Scope_NamesTheCompanyWithTheTextOfTheSpec()
        => Assert.Equal("Este cambio afecta a todas las personas de la compañía Acme Norte.", AssistantConfigurationNotice.Scope("Acme Norte"));

    [Fact]
    public void Scope_KeepsTheNameAsLiteralPlainText()
    {
        var text = AssistantConfigurationNotice.Scope("<b>x</b>");

        Assert.Contains("la compañía <b>x</b>.", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Scope_WithoutAName_FallsBackToYourCompany_NeverToTheWholeInstance(string? name)
        => Assert.Equal("Este cambio afecta a todas las personas de su compañía.", AssistantConfigurationNotice.Scope(name));

    [Theory]
    [InlineData("Acme Norte")]
    [InlineData(null)]
    public void NoText_ClaimsTheConfigurationIsForTheWholeCompanyOrInstance(string? name)
    {
        var texts = new[] { AssistantConfigurationNotice.Scope(name), AssistantConfigurationNotice.ResetWarning(name) };

        Assert.All(texts, text =>
        {
            Assert.DoesNotContain("toda la empresa", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("toda la instancia", text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ResetWarning_NamesTheCompany_AndKeepsTheWarningAboutSomeoneElsesChange()
    {
        var text = AssistantConfigurationNotice.ResetWarning("Acme Norte");

        Assert.Contains("compañía Acme Norte", text, StringComparison.Ordinal);
        Assert.Contains("aunque otro responsable", text, StringComparison.Ordinal);
        Assert.Contains("incluida la clave", text, StringComparison.Ordinal);
        Assert.Contains("valores del entorno", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetWarning_WithoutAName_SaysYourCompany()
        => Assert.Contains("de su compañía", AssistantConfigurationNotice.ResetWarning(null), StringComparison.Ordinal);
}
