namespace TimeTracker.Api.Security;

/// <summary>
/// Añade las cabeceras de seguridad a todas las respuestas, errores incluidos (DD-20). Se registran en
/// <c>OnStarting</c> y no al entrar: <c>ExceptionHandler</c> limpia las cabeceras al tratar una excepción y
/// las fijadas antes se perderían en el 500.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // La API solo devuelve JSON: no necesita cargar nada ni ser incrustada. Sin comodines ni unsafe-inline.
    private const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            var headers = httpContext.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = ContentSecurityPolicy;

            // Las respuestas con datos de la persona no se guardan en cachés intermedias ni del navegador.
            if (httpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}

public static class SecurityHeadersApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.UseMiddleware<SecurityHeadersMiddleware>();
}
