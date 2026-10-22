using System.Globalization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using TimeTracker.Api.Registration;
using TimeTracker.Api.Security;
using TimeTracker.Application.Companies;

namespace TimeTracker.Api.Authentication;

/// <summary>
/// <c>POST /api/auth/register-company</c>: autorregistro de una compañía y su primer Manager con un código de invitación (DD-19).
/// La ruta solo existe con <c>Registration:Enabled</c> (cerrada por defecto): apagada se comporta como una ruta inexistente.
/// </summary>
public static class RegistrationEndpoints
{
    public const string Route = "/api/auth/register-company";

    // Límite de diseño explícito que la spec no fija: un cuerpo válido mide menos de 1 KiB y esta ruta es anónima.
    public const long MaxRequestBodyBytes = 4 * 1024;

    public const string InviteCodeDetail = "El código de invitación no es válido.";
    public const string DuplicateNameDetail = "Ya existe una compañía con ese nombre.";
    public const string CreatedWithoutSessionDetail = "La compañía y la cuenta se han creado, pero no se ha podido iniciar la sesión. No repita el registro: inicie sesión con su correo electrónico y su contraseña.";
    public const string DuplicateEmailDetail = "Ya existe una cuenta con ese correo electrónico.";

    public static IEndpointRouteBuilder MapRegistrationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // Apagado, ni siquiera se registra: la petición recibe la respuesta de cualquier ruta inexistente y no se evalúa ningún código.
        if (!endpoints.ServiceProvider.GetRequiredService<IOptions<RegistrationOptions>>().Value.Enabled)
        {
            return endpoints;
        }

        endpoints.MapPost(Route, RegisterAsync)
            .WithTags("Auth")
            .AllowAnonymous()
            .SkipCompanyContext()
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodyBytes))
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.RegisterCompanyPolicy)
            .Produces<LoginResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    // No lee la identidad del usuario ni la compañía del token: el registro es anónimo aunque la petición lleve un token (DD-7).
    private static async Task<IResult> RegisterAsync(
        RegisterCompanyRequest? request,
        RegisterCompanyUseCase useCase,
        RegistrationGlobalLimiter globalLimiter,
        HttpContext httpContext,
        TimeProvider clock,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(RegistrationLog.Category);
        if (request is null)
        {
            RegistrationLog.Rejected(logger, RegistrationAttemptResult.RejectedByValidation, clock.GetUtcNow());
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Solicitud no válida",
                detail: "El cuerpo de la solicitud es obligatorio.");
        }

        // Tope global de altas (todas las IP): se reserva antes de evaluar el código y se libera si no se crea nada, de modo que solo
        // cuentan las altas. Con un código filtrado limita cuántas compañías puede crear alguien en una ventana.
        if (globalLimiter.Reserve(out var retryAfter) is not { } reservation)
        {
            RegistrationLog.Rejected(logger, RegistrationAttemptResult.RejectedByRateLimit, clock.GetUtcNow());
            httpContext.Response.Headers.RetryAfter = RateLimitingServiceCollectionExtensions.RetryAfterSeconds(retryAfter).ToString(CultureInfo.InvariantCulture);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Demasiadas solicitudes",
                detail: RateLimitingServiceCollectionExtensions.RegisterCompanyDetail);
        }

        RegisterCompanyResult result;
        try
        {
            result = await useCase.ExecuteAsync(
                new RegisterCompanyCommand(request.CompanyName, request.ManagerName, request.Email, request.Password, request.InviteCode),
                cancellationToken);
        }
        catch
        {
            globalLimiter.Release(reservation);
            throw;
        }

        if (result is not (RegisterCompanyResult.Created or RegisterCompanyResult.CreatedWithoutSession))
        {
            globalLimiter.Release(reservation);
        }

        switch (result)
        {
            case RegisterCompanyResult.Created created:
                RegistrationLog.Created(logger, created.CompanyId, clock.GetUtcNow());
                return TypedResults.Json(LoginResponse.From(created.Session), statusCode: StatusCodes.Status201Created);
            case RegisterCompanyResult.CreatedWithoutSession withoutSession:
                // La cuenta existe: 409 con un detalle propio (el contrato no cambia y la Web ya muestra el detalle del 409) en lugar de un 500.
                RegistrationLog.CreatedWithoutSession(logger, withoutSession.CompanyId, clock.GetUtcNow());
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: CreatedWithoutSessionDetail);
            case RegisterCompanyResult.InvalidInviteCode:
                RegistrationLog.Rejected(logger, RegistrationAttemptResult.RejectedByInviteCode, clock.GetUtcNow());
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Acceso denegado", detail: InviteCodeDetail);
            case RegisterCompanyResult.Invalid invalid:
                RegistrationLog.Rejected(logger, RegistrationAttemptResult.RejectedByValidation, clock.GetUtcNow());
                return TypedResults.ValidationProblem(
                    invalid.Errors.GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()),
                    title: "Solicitud no válida");
            case RegisterCompanyResult.DuplicateName:
                RegistrationLog.Rejected(logger, RegistrationAttemptResult.RejectedByDuplicate, clock.GetUtcNow());
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: DuplicateNameDetail);
            case RegisterCompanyResult.DuplicateEmail:
                RegistrationLog.Rejected(logger, RegistrationAttemptResult.RejectedByDuplicate, clock.GetUtcNow());
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: DuplicateEmailDetail);
            default:
                throw new InvalidOperationException("Resultado del registro no contemplado.");
        }
    }
}
