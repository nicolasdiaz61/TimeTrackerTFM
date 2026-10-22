using System.Security.Claims;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Con el cambio de contraseña pendiente toda ruta distinta de la página de cambio (y del inicio de sesión) lleva a ella sin
/// llamar a la API, que respondería <c>403</c>. Es solo presentación: la API lo impone con su propio middleware.
/// </summary>
public static class PasswordChangeRedirect
{
    public const string ChangePasswordRoute = "/cambiar-contrasena";

    private const string LoginRoute = "/login";

    /// <summary>Destino de la redirección para la ruta relativa indicada (con consulta), o <see langword="null"/> si no hace falta.</summary>
    public static string? TargetFor(ClaimsPrincipal user, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(relativePath);

        if (user.Identity?.IsAuthenticated != true || !SessionClaims.IsPasswordChangePending(user))
        {
            return null;
        }

        var path = relativePath.Length == 0 ? ReturnUrl.DefaultTarget : relativePath;
        var routeEnd = path.IndexOfAny(['?', '#']);
        var route = routeEnd < 0 ? path : path[..routeEnd];
        if (IsRoute(route, ChangePasswordRoute) || IsRoute(route, LoginRoute))
        {
            return null;
        }

        return path == ReturnUrl.DefaultTarget || !ReturnUrl.IsSafe(path)
            ? ChangePasswordRoute
            : WithReturnUrl(path);
    }

    /// <summary>Página de cambio conservando un destino posterior seguro; sin destino seguro, la página a secas.</summary>
    public static string WithReturnUrl(string? returnUrl)
        => ReturnUrl.IsSafe(returnUrl) && returnUrl != ReturnUrl.DefaultTarget
            ? $"{ChangePasswordRoute}?returnUrl={Uri.EscapeDataString(returnUrl!)}"
            : ChangePasswordRoute;

    private static bool IsRoute(string route, string expected)
        => string.Equals(route.TrimEnd('/'), expected, StringComparison.OrdinalIgnoreCase);
}
