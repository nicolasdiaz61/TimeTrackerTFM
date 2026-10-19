using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Api.Security;

public static class RateLimitingServiceCollectionExtensions
{
    public const string LoginPolicy = "login";

    public const string AssistantPolicy = "assistant";

    public const int LoginPermitLimit = 10;

    // Sin dirección remota (servidor en memoria de las pruebas) todo cae en una partición única.
    private const string NoAddressPartition = "sin-ip";

    private const int FallbackAssistantPermitLimit = 5;

    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan AssistantWindow = TimeSpan.FromMinutes(1);

    private const string LoginDetail = "Espere un minuto antes de volver a intentarlo.";

    private const string AssistantDetail = "Ha realizado demasiadas consultas al asistente en poco tiempo. Espere un minuto antes de volver a intentarlo.";

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
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
            // Ventana fija por usuario autenticado (claim sub), no por IP: el limitador corre tras la autorización (DD-1).
            // Cuentan todas las peticiones que llegan al endpoint, también las que acaban en 400 o 503.
            options.AddPolicy(AssistantPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst("sub")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? NoAddressPartition,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AssistantPermitLimit(httpContext),
                    Window = AssistantWindow,
                    QueueLimit = 0,
                }));
            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    /// <summary>Segundos de <c>Retry-After</c>: entero y siempre positivo.</summary>
    internal static int RetryAfterSeconds(TimeSpan wait) => Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));

    // Con el asistente deshabilitado las claves numéricas no se validan: un valor fuera de rango no debe romper el limitador.
    private static int AssistantPermitLimit(HttpContext httpContext)
    {
        var limit = httpContext.RequestServices.GetRequiredService<IOptions<AiOptions>>().Value.RequestsPerMinute;
        return limit is >= 1 and <= 60 ? limit : FallbackAssistantPermitLimit;
    }

    // El cuerpo se escribe aquí y no en UseStatusCodePages porque el detalle en español solo lo conoce este límite (V9).
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = RetryAfterSeconds(retryAfter).ToString(CultureInfo.InvariantCulture);
        }

        // El texto depende de la política del endpoint rechazado: login y asistente tienen detalles distintos.
        var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiadas solicitudes",
                Detail = policy == AssistantPolicy ? AssistantDetail : LoginDetail,
            },
        });
    }
}
