using TimeTracker.Web.Auth;

namespace TimeTracker.Web.Tests;

public class ReturnUrlTests
{
    [Fact]
    public void DefaultTarget_IsTheHomePageWhereTheClockPlayerLives()
        => Assert.Equal("/", ReturnUrl.DefaultTarget);

    [Theory]
    [InlineData("/")]
    [InlineData("/equipo")]
    [InlineData("/fichajes")]
    public void IsSafe_AcceptsRelativeRoutesOfTheApplication(string value)
        => Assert.True(ReturnUrl.IsSafe(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("//ejemplo.test")]
    [InlineData("/\\ejemplo.test")]
    [InlineData("https://ejemplo.test")]
    [InlineData("/\t/ejemplo.test")]
    public void IsSafe_RejectsOpenRedirects(string? value)
        => Assert.False(ReturnUrl.IsSafe(value));
}
