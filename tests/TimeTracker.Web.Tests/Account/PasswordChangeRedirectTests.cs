using System.Security.Claims;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Account;

public class PasswordChangeRedirectTests
{
    private static ClaimsPrincipal Pending() => Tokens.Principal(("name", "Ana"), ("mcp", "true"));

    private static ClaimsPrincipal Complete() => Tokens.Principal(("name", "Ana"));

    [Theory]
    [InlineData("/fichajes", "/cambiar-contrasena?returnUrl=%2Ffichajes")]
    [InlineData("/equipo/alta", "/cambiar-contrasena?returnUrl=%2Fequipo%2Falta")]
    [InlineData("/fichajes?mes=2026-10", "/cambiar-contrasena?returnUrl=%2Ffichajes%3Fmes%3D2026-10")]
    [InlineData("/asistente", "/cambiar-contrasena?returnUrl=%2Fasistente")]
    public void ASessionWithAPendingChange_IsSentToTheChangePage_KeepingTheRequestedRoute(string path, string expected)
        => Assert.Equal(expected, PasswordChangeRedirect.TargetFor(Pending(), path));

    [Theory]
    [InlineData("/")]
    [InlineData("")]
    public void TheHomeRoute_IsRedirectedWithoutReturnUrl(string path)
        => Assert.Equal("/cambiar-contrasena", PasswordChangeRedirect.TargetFor(Pending(), path));

    [Theory]
    [InlineData("/cambiar-contrasena")]
    [InlineData("/Cambiar-Contrasena")]
    [InlineData("/cambiar-contrasena?returnUrl=%2Fequipo")]
    [InlineData("/login")]
    [InlineData("/login?returnUrl=%2Fequipo")]
    public void TheChangePageAndLogin_AreNeverRedirected(string path)
        => Assert.Null(PasswordChangeRedirect.TargetFor(Pending(), path));

    [Theory]
    [InlineData("/cambiar-contrasenas")]
    [InlineData("/cambiar-contrasena/otra")]
    [InlineData("/login-falso")]
    public void ARouteThatOnlyStartsLikeTheExemptOnes_IsRedirected(string path)
        => Assert.NotNull(PasswordChangeRedirect.TargetFor(Pending(), path));

    [Theory]
    [InlineData("/fichajes")]
    [InlineData("/")]
    [InlineData("/cambiar-contrasena")]
    public void WithoutAPendingChange_NothingIsRedirected(string path)
        => Assert.Null(PasswordChangeRedirect.TargetFor(Complete(), path));

    [Fact]
    public void AnAnonymousVisitor_IsNotRedirected()
        => Assert.Null(PasswordChangeRedirect.TargetFor(new ClaimsPrincipal(new ClaimsIdentity()), "/fichajes"));

    [Fact]
    public void AnUnsafeRequestedRoute_IsNotKeptAsReturnUrl()
        => Assert.Equal("/cambiar-contrasena", PasswordChangeRedirect.TargetFor(Pending(), "//ejemplo.test"));
}
