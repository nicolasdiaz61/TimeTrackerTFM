using Microsoft.AspNetCore.Builder;

namespace TimeTracker.Api.Errors;

public static class ProblemDetailsServiceCollectionExtensions
{
    private static readonly Dictionary<int, string> SpanishTitles = new()
    {
        [StatusCodes.Status400BadRequest] = "Solicitud no válida",
        [StatusCodes.Status401Unauthorized] = "Se requiere autenticación",
        [StatusCodes.Status403Forbidden] = "Acceso denegado",
        [StatusCodes.Status404NotFound] = "Recurso no encontrado",
        [StatusCodes.Status405MethodNotAllowed] = "Método no permitido",
        [StatusCodes.Status429TooManyRequests] = "Demasiadas solicitudes",
    };

    public static IServiceCollection AddDomainProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // El 500 es genérico y en español: nunca lleva el mensaje ni la traza de la excepción (A05, API8).
            if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
            {
                context.ProblemDetails.Title = "Se produjo un error inesperado";
                context.ProblemDetails.Detail = null;
            }
            else if (context.ProblemDetails.Detail is null
                && context.ProblemDetails.Status is { } status
                && SpanishTitles.TryGetValue(status, out var title))
            {
                // Errores de acceso sin cuerpo propio (401, 403, 404, 405, 429): título en español y nunca detalle.
                context.ProblemDetails.Title = title;
            }
        });
        services.AddExceptionHandler<DomainRuleExceptionHandler>();

        // Un cuerpo ilegible (JSON roto) es un error del cliente. Por defecto solo Development lanza
        // BadHttpRequestException al enlazar; en Production responde 400 sin cuerpo. Se lanza siempre para que el manejador
        // de excepciones dé el mismo 400 problem+json en ambos entornos, y el selector evita que sea un 500 (V18).
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.Configure<ExceptionHandlerOptions>(options => options.StatusCodeSelector =
            exception => exception is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError);

        return services;
    }
}
