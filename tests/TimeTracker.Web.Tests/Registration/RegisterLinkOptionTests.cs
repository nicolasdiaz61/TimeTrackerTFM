using Microsoft.Extensions.Configuration;

using TimeTracker.Web.Registration;

namespace TimeTracker.Web.Tests.Registration;

/// <summary>El enlace «Crear una compañía» del login solo se muestra si la configuración de la Web lo activa con exactamente un booleano true (ADR 0033).</summary>
public class RegisterLinkOptionTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void ATrueValue_ShowsTheLink(string value)
        => Assert.True(RegisterLinkOption.IsEnabled(Config(("Features:ShowRegisterLink", value))));

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("si")]
    [InlineData("quizas")]
    public void AnythingElse_HidesTheLink(string value)
        => Assert.False(RegisterLinkOption.IsEnabled(Config(("Features:ShowRegisterLink", value))));

    [Fact]
    public void AnAbsentValue_HidesTheLink()
        => Assert.False(RegisterLinkOption.IsEnabled(Config()));

    [Fact]
    public void TheKeyNameIsFixed()
        => Assert.Equal("Features:ShowRegisterLink", RegisterLinkOption.Key);
}
