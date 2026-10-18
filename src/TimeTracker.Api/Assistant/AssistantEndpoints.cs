using System.Globalization;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using TimeTracker.Api.Security;
using TimeTracker.Application.Assistant;

namespace TimeTracker.Api.Assistant;

public static class AssistantEndpoints
{
    public const string Route = "/api/asistente";

    // El cuerpo válido más grande (mensaje 500 + historial 8000 caracteres) cabe de sobra; el resto se rechaza con 413 sin leerlo (DD-15).
    public const long MaxRequestBodyBytes = 32 * 1024;

    public const string DisabledDetail = "El asistente no está disponible en este entorno.";
    public const string UnavailableDetail = "El asistente no está disponible en este momento. Inténtelo de nuevo más tarde.";
    public const string DailyLimitDetail = "Ha alcanzado el límite diario de consultas al asistente. Inténtelo de nuevo mañana.";

    // Mismo título en los dos 503: solo cambia el detalle (DD-16).
    private const string UnavailableTitle = "Asistente no disponible";

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Protegido por la política de respaldo (cualquier usuario autenticado); el limitador va tras la autorización (DD-1).
        endpoints.MapPost(Route, AskAsync)
            .WithTags("Assistant")
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodyBytes))
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.AssistantPolicy)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<Ok<AssistantResponse>, ProblemHttpResult>> AskAsync(
        AssistantRequest? request,
        AskAssistantUseCase useCase,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "Solicitud no válida", "El cuerpo de la solicitud es obligatorio.");
        }

        var history = request.History?.Select(turn => new AssistantTurnInput(turn?.Role, turn?.Text)).ToList();
        var result = await useCase.ExecuteAsync(new AskAssistantCommand(request.Message, history), cancellationToken);

        switch (result)
        {
            case AskAssistantResult.Answered answered:
                return TypedResults.Ok(new AssistantResponse(answered.Reply));
            case AskAssistantResult.Rejected rejected:
                return Problem(StatusCodes.Status400BadRequest, "Solicitud no válida", rejected.Error);
            case AskAssistantResult.QuotaExceeded exceeded:
                response.Headers.RetryAfter = RateLimitingServiceCollectionExtensions.RetryAfterSeconds(exceeded.RetryAfter)
                    .ToString(CultureInfo.InvariantCulture);
                return Problem(StatusCodes.Status429TooManyRequests, "Demasiadas solicitudes", DailyLimitDetail);
            case AskAssistantResult.Unavailable { Disabled: true }:
                return Problem(StatusCodes.Status503ServiceUnavailable, UnavailableTitle, DisabledDetail);
            default:
                return Problem(StatusCodes.Status503ServiceUnavailable, UnavailableTitle, UnavailableDetail);
        }
    }

    private static ProblemHttpResult Problem(int status, string title, string detail)
        => TypedResults.Problem(detail: detail, statusCode: status, title: title);
}
