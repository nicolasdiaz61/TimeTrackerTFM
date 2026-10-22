using Microsoft.AspNetCore.Authorization;

using TimeTracker.Api.Authentication;

namespace TimeTracker.Api.Security;

/// <summary>
/// Marca un endpoint que una persona con el cambio de contraseña pendiente (claim <c>mcp</c>) puede usar. Solo lo llevan
/// <c>GET /api/auth/me</c> y <c>POST /api/auth/change-password</c>; una prueba de arquitectura lo exige.
/// </summary>
public sealed class AllowWhilePasswordChangePendingMetadata;

public static class AllowWhilePasswordChangePendingExtensions
{
    public static TBuilder AllowWhilePasswordChangePending<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new AllowWhilePasswordChangePendingMetadata());
}

/// <summary>
/// Cambio de contraseña obligatorio de verdad (DD-21): una petición autenticada con <c>mcp</c> igual a <c>"true"</c> (valor estricto,
/// no mera presencia) recibe 403 en todo endpoint que no lleve <see cref="AllowWhilePasswordChangePendingMetadata"/>. Decide solo por el
/// claim, sin consultar la base de datos. Va tras la autenticación y antes de la autorización, de modo que ningún caso de uso ni
/// política se ejecuta. Los endpoints anónimos (inicio de sesión, registro, salud) no sirven datos de la persona y quedan fuera.
/// </summary>
public sealed class PasswordChangePendingMiddleware(RequestDelegate next, IProblemDetailsService problemDetails)
{
    private const string Detail = "Debe cambiar su contraseña antes de continuar.";

    public Task InvokeAsync(HttpContext context)
    {
        return IsPending(context) && !IsExempt(context.GetEndpoint()) ? RejectAsync(context) : next(context);
    }

    private static bool IsPending(HttpContext context)
        => context.User.Identity?.IsAuthenticated == true
            && string.Equals(context.User.FindFirst(JwtDefaults.PasswordChangePendingClaim)?.Value, JwtDefaults.PasswordChangePendingValue, StringComparison.Ordinal);

    private static bool IsExempt(Endpoint? endpoint)
        => endpoint?.Metadata.GetMetadata<AllowWhilePasswordChangePendingMetadata>() is not null
            || endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    private async Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = StatusCodes.Status403Forbidden, Title = "Acceso denegado", Detail = Detail },
        });
    }
}

public static class PasswordChangePendingMiddlewareExtensions
{
    public static IApplicationBuilder UsePasswordChangePending(this IApplicationBuilder app) => app.UseMiddleware<PasswordChangePendingMiddleware>();
}
