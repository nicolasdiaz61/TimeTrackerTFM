using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

using TimeTracker.Api.Registration;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Api.Security;

public static class RateLimitingServiceCollectionExtensions
{
    public const string LoginPolicy = "login";

    public const string RegisterCompanyPolicy = "register-company";

    public const string AssistantPolicy = "assistant";

    public const string AssistantConfigTestPolicy = "assistant-config-test";

    public const string EmployeeCreatePolicy = "employee-create";

    public const string TemporaryPasswordPolicy = "temporary-password";

    public const string ChangePasswordPolicy = "change-password";

    public const int LoginPermitLimit = 10;

    // Constantes de código, como el login: 5 intentos por hora y por IP (DD-19).
    public const int RegisterCompanyPermitLimit = 5;

    // Sin dirección remota (servidor en memoria de las pruebas) todo cae en una partición única.
    private const string NoAddressPartition = "sin-ip";

    private const int FallbackAssistantPermitLimit = 5;

    // Constantes de código, por usuario autenticado y minuto (employee-management): 30 altas, 30 regeneraciones y 5 cambios de contraseña.
    private const int EmployeeCreatePermitLimit = 30;

    private const int TemporaryPasswordPermitLimit = 30;

    private const int ChangePasswordPermitLimit = 5;

    // Constante de código: los límites de la prueba de conexión no son editables (DD-15).
    private const int AssistantConfigTestPermitLimit = 3;

    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan RegisterCompanyWindow = TimeSpan.FromHours(1);

    private static readonly TimeSpan AssistantWindow = TimeSpan.FromMinutes(1);

    private const string LoginDetail = "Espere un minuto antes de volver a intentarlo.";

    private const string RegisterCompanyDetail = "Ha realizado demasiados intentos de registro. Inténtelo de nuevo más tarde.";

    private const string AssistantDetail = "Ha realizado demasiadas consultas al asistente en poco tiempo. Espere un minuto antes de volver a intentarlo.";

    private const string AssistantConfigTestDetail = "Ha realizado demasiadas pruebas de conexión. Espere un minuto antes de volver a intentarlo.";

    private const string EmployeeCreateDetail = "Ha realizado demasiadas altas de personas en poco tiempo. Espere un minuto antes de volver a intentarlo.";

    private const string TemporaryPasswordDetail = "Ha regenerado demasiadas contraseñas temporales en poco tiempo. Espere un minuto antes de volver a intentarlo.";

    private const string ChangePasswordDetail = "Ha realizado demasiados intentos de cambio de contraseña. Espere un minuto antes de volver a intentarlo.";

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
            // Registro de compañías: anónimo, por IP (con ForwardedHeaders la IP real del cliente) y 5 por hora. Cuentan todos los
            // intentos, también los rechazados por el código, y el sexto se rechaza sin evaluar el código (DD-19).
            options.AddPolicy(RegisterCompanyPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? NoAddressPartition,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = RegisterCompanyPermitLimit,
                    Window = RegisterCompanyWindow,
                    QueueLimit = 0,
                }));
            // Ventana fija por usuario autenticado (claim sub), no por IP: el limitador corre tras la autorización (DD-1).
            // Cuentan todas las peticiones que llegan al endpoint, también las que acaban en 400 o 503.
            options.AddPolicy(AssistantPolicy, httpContext => PerUser(httpContext, AssistantPermitLimit(httpContext)));
            // Mismo reparto que el chat, pero con tope fijo y contador propio: probar la conexión no gasta consultas del chat.
            options.AddPolicy(AssistantConfigTestPolicy, httpContext => PerUser(httpContext, AssistantConfigTestPermitLimit));
            // Alta, regeneración y cambio de contraseña: por usuario del token y con contador propio cada uno. Cuentan todas las peticiones que
            // llegan al endpoint, también las que acaban en 400, 404 o 409.
            options.AddPolicy(EmployeeCreatePolicy, httpContext => PerUser(httpContext, EmployeeCreatePermitLimit));
            options.AddPolicy(TemporaryPasswordPolicy, httpContext => PerUser(httpContext, TemporaryPasswordPermitLimit));
            options.AddPolicy(ChangePasswordPolicy, httpContext => PerUser(httpContext, ChangePasswordPermitLimit));
            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> PerUser(HttpContext httpContext, int permitLimit) => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirst("sub")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? NoAddressPartition,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = AssistantWindow,
            QueueLimit = 0,
        });

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

        // El texto depende de la política del endpoint rechazado: login, chat y prueba de conexión tienen detalles distintos.
        var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        if (policy == RegisterCompanyPolicy)
        {
            // Solo el resultado y el instante, como el resto del registro del autorregistro (sin IP, código ni datos del cuerpo).
            var services = context.HttpContext.RequestServices;
            RegistrationLog.Rejected(
                services.GetRequiredService<ILoggerFactory>().CreateLogger(RegistrationLog.Category),
                RegistrationAttemptResult.RejectedByRateLimit,
                services.GetRequiredService<TimeProvider>().GetUtcNow());
        }

        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiadas solicitudes",
                Detail = DetailFor(policy),
            },
        });
    }

    private static string DetailFor(string? policy) => policy switch
    {
        AssistantPolicy => AssistantDetail,
        AssistantConfigTestPolicy => AssistantConfigTestDetail,
        RegisterCompanyPolicy => RegisterCompanyDetail,
        EmployeeCreatePolicy => EmployeeCreateDetail,
        TemporaryPasswordPolicy => TemporaryPasswordDetail,
        ChangePasswordPolicy => ChangePasswordDetail,
        _ => LoginDetail,
    };
}
