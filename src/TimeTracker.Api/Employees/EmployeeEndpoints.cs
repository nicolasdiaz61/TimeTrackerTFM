using TimeTracker.Api.Security;
using TimeTracker.Application.Authentication;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Team;

namespace TimeTracker.Api.Employees;

/// <summary>
/// Alta de personas y regeneración de la contraseña temporal (DD-21). Solo para Managers: el rol se exige en el grupo, de modo que un
/// Employee recibe 403 antes de que se evalúe el cuerpo o se mire a qué persona se refiere la ruta. La compañía sale siempre del token. La
/// respuesta con la contraseña temporal lleva <c>Cache-Control: no-store</c> por <see cref="SecurityHeadersMiddleware"/>, que lo fija en toda
/// respuesta de <c>/api</c>: aquí no se repite.
/// </summary>
public static class EmployeeEndpoints
{
    public const string DuplicateEmailDetail = "Ya existe una cuenta con ese correo electrónico.";
    public const string CapReachedDetail = "Se ha alcanzado el número máximo de personas de la compañía.";
    public const string BusyDetail = "No se pudo completar el alta porque hay demasiadas altas simultáneas. Inténtelo de nuevo.";
    public const string RegenerationConflictDetail = "No se puede regenerar la contraseña temporal: la persona ya cambió su contraseña o su cuenta ha cambiado.";

    public static IServiceCollection AddEmployees(this IServiceCollection services)
    {
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddScoped<CreateTeamMemberUseCase>();
        services.AddScoped<ResetTemporaryPasswordUseCase>();

        return services;
    }

    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/employees").WithTags("Employees").RequireAuthorization(AuthorizationPolicies.Manager)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost(string.Empty, CreateAsync)
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.EmployeeCreatePolicy)
            .Produces<CreateEmployeeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // El id solo se enlaza si es un GUID: otro texto no casa con la ruta y da el mismo 404 que una persona ajena.
        group.MapPost("{memberId:guid}/temporary-password", RegenerateAsync)
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.TemporaryPasswordPolicy)
            .Produces<TemporaryPasswordResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateEmployeeRequest? request,
        CreateTeamMemberUseCase useCase,
        ICurrentUser currentUser,
        TimeProvider clock,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(EmployeeLog.Category);
        var managerId = currentUser.EmployeeId;
        if (request is null)
        {
            EmployeeLog.Operation(logger, EmployeeOperationResult.RejectedByValidation, managerId, null, clock.GetUtcNow());
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Solicitud no válida",
                detail: "El cuerpo de la solicitud es obligatorio.");
        }

        var result = await useCase.ExecuteAsync(new CreateTeamMemberCommand(request.DisplayName, request.Email, request.Role, request.ManagerId), cancellationToken);

        switch (result)
        {
            case CreateTeamMemberResult.Created created:
                EmployeeLog.Operation(logger, EmployeeOperationResult.Created, managerId, created.User.Id, clock.GetUtcNow());
                return TypedResults.Json(CreateEmployeeResponse.From(created.User, created.TemporaryPassword), statusCode: StatusCodes.Status201Created);
            case CreateTeamMemberResult.Invalid invalid:
                EmployeeLog.Operation(logger, EmployeeOperationResult.RejectedByValidation, managerId, null, clock.GetUtcNow());
                return TypedResults.ValidationProblem(
                    invalid.Errors.GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()),
                    title: "Solicitud no válida");
            case CreateTeamMemberResult.DuplicateEmail:
                EmployeeLog.Operation(logger, EmployeeOperationResult.RejectedByDuplicate, managerId, null, clock.GetUtcNow());
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: DuplicateEmailDetail);
            case CreateTeamMemberResult.CapReached:
                EmployeeLog.Operation(logger, EmployeeOperationResult.RejectedByCap, managerId, null, clock.GetUtcNow());
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: CapReachedDetail);
            case CreateTeamMemberResult.Busy:
                EmployeeLog.Operation(logger, EmployeeOperationResult.RejectedByContention, managerId, null, clock.GetUtcNow());
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: BusyDetail);
            default:
                throw new InvalidOperationException("Resultado del alta no contemplado.");
        }
    }

    private static async Task<IResult> RegenerateAsync(
        Guid memberId,
        ResetTemporaryPasswordUseCase useCase,
        ICurrentUser currentUser,
        TimeProvider clock,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(EmployeeLog.Category);
        var managerId = currentUser.EmployeeId;

        switch (await useCase.ExecuteAsync(memberId, cancellationToken))
        {
            case ResetTemporaryPasswordResult.Regenerated regenerated:
                EmployeeLog.Operation(logger, EmployeeOperationResult.Regenerated, managerId, memberId, clock.GetUtcNow());
                return TypedResults.Json(new TemporaryPasswordResponse(regenerated.TemporaryPassword));
            case ResetTemporaryPasswordResult.NotFound:
                // Ajena, inexistente o de otro responsable: el mismo 404 en los tres casos.
                EmployeeLog.Operation(logger, EmployeeOperationResult.NotFound, managerId, null, clock.GetUtcNow());
                return TypedResults.NotFound();
            case ResetTemporaryPasswordResult.Conflict:
                EmployeeLog.Operation(logger, EmployeeOperationResult.Conflict, managerId, memberId, clock.GetUtcNow());
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: RegenerationConflictDetail);
            default:
                throw new InvalidOperationException("Resultado de la regeneración no contemplado.");
        }
    }
}
