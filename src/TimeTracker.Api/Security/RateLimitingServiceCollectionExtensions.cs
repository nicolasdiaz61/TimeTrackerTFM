using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace TimeTracker.Api.Security;

public static class RateLimitingServiceCollectionExtensions
{
    public const string LoginPolicy = "login";

    public const int LoginPermitLimit = 10;

    // Sin dirección remota (servidor en memoria de las pruebas) todo cae en una partición única.
    private const string NoAddressPartition = "sin-ip";

    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Ventana fija por IP: cuenta todos los intentos, correctos o no, y no encola ninguno (DD-19).
            options.AddPolicy(LoginPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? NoAddressPartition,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LoginPermitLimit,
                    Window = LoginWindow,
                    QueueLimit = 0,
                }));
            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    // El cuerpo se escribe aquí y no en UseStatusCodePages porque el detalle en español solo lo conoce este límite (V9).
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiadas solicitudes",
                Detail = "Espere un minuto antes de volver a intentarlo.",
            },
        });
    }
}
