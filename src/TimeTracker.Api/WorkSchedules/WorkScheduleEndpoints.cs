using Microsoft.AspNetCore.Http.HttpResults;

using TimeTracker.Api.Security;
using TimeTracker.Application.WorkSchedules;

namespace TimeTracker.Api.WorkSchedules;

/// <summary>
/// Jornadas de trabajo. El alta, la edición, el borrado y el listado son solo para Managers (el rol se exige por ruta: un empleado
/// recibe 403 antes de mirar el cuerpo o el identificador). <c>mine</c> es para cualquier sesión. La compañía sale siempre del token.
/// </summary>
public static class WorkScheduleEndpoints
{
    public const string DuplicateNameDetail = "Ya existe una jornada con ese nombre en la compañía.";
    public const string CapReachedDetail = "Se ha alcanzado el número máximo de jornadas de la compañía.";
    public const string InUseDetail = "No se puede borrar la jornada porque está asignada a alguna persona. Retire primero las asignaciones.";
    public const string AssignmentConflictDetail = "No se pudo asignar la jornada porque la persona ha cambiado. Inténtelo de nuevo.";

    public static IServiceCollection AddWorkSchedules(this IServiceCollection services)
    {
        services.AddScoped<ListWorkSchedulesUseCase>();
        services.AddScoped<CreateWorkScheduleUseCase>();
        services.AddScoped<UpdateWorkScheduleUseCase>();
        services.AddScoped<DeleteWorkScheduleUseCase>();
        services.AddScoped<AssignWorkScheduleUseCase>();
        services.AddScoped<GetMyWorkScheduleUseCase>();

        return services;
    }

    public static IEndpointRouteBuilder MapWorkScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/work-schedules").WithTags("WorkSchedules")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Cualquier persona autenticada ve su propia jornada (la política por defecto exige sesión).
        group.MapGet("mine", MineAsync)
            .Produces<WorkScheduleResponse>()
            .Produces(StatusCodes.Status204NoContent);

        var managers = group.MapGroup(string.Empty).RequireAuthorization(AuthorizationPolicies.Manager)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        managers.MapGet(string.Empty, async (ListWorkSchedulesUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Ok(WorkScheduleListResponse.From(await useCase.ExecuteAsync(cancellationToken))))
            .Produces<WorkScheduleListResponse>();

        managers.MapPost(string.Empty, CreateAsync)
            .Produces<WorkScheduleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // El id solo se enlaza si es un GUID: otro texto no casa con la ruta y da el mismo 404 que una jornada ajena.
        managers.MapPut("{id:guid}", UpdateAsync)
            .Produces<WorkScheduleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        managers.MapDelete("{id:guid}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    /// <summary>Asignación de jornada a una persona del equipo; la mapea <c>TeamEndpoints</c> bajo <c>/api/team/members</c>.</summary>
    public static async Task<IResult> AssignAsync(
        Guid memberId,
        AssignWorkScheduleRequest? request,
        AssignWorkScheduleUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        return await useCase.ExecuteAsync(memberId, request.WorkScheduleId, cancellationToken) switch
        {
            AssignWorkScheduleOutcome.Assigned => TypedResults.NoContent(),
            AssignWorkScheduleOutcome.NotFound => TypedResults.NotFound(),
            AssignWorkScheduleOutcome.Conflict => Conflict(AssignmentConflictDetail),
            _ => throw new InvalidOperationException("Resultado de la asignación no contemplado."),
        };
    }

    private static async Task<Results<Ok<WorkScheduleResponse>, NoContent>> MineAsync(GetMyWorkScheduleUseCase useCase, CancellationToken cancellationToken)
        => await useCase.ExecuteAsync(cancellationToken) is { } schedule
            ? TypedResults.Ok(WorkScheduleResponse.From(schedule, 0))
            : TypedResults.NoContent();

    private static async Task<IResult> CreateAsync(WorkScheduleRequest? request, CreateWorkScheduleUseCase useCase, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var result = await useCase.ExecuteAsync(request.ToCommand(), cancellationToken);

        return result is SaveWorkScheduleResult.Saved saved
            ? TypedResults.Created($"/api/work-schedules/{saved.View.Schedule.Id}", WorkScheduleResponse.From(saved.View))
            : Failure(result);
    }

    private static async Task<IResult> UpdateAsync(Guid id, WorkScheduleRequest? request, UpdateWorkScheduleUseCase useCase, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var result = await useCase.ExecuteAsync(id, request.ToCommand(), cancellationToken);

        return result is SaveWorkScheduleResult.Saved saved ? TypedResults.Ok(WorkScheduleResponse.From(saved.View)) : Failure(result);
    }

    private static async Task<IResult> DeleteAsync(Guid id, DeleteWorkScheduleUseCase useCase, CancellationToken cancellationToken)
        => await useCase.ExecuteAsync(id, cancellationToken) switch
        {
            DeleteWorkScheduleOutcome.Deleted => TypedResults.NoContent(),
            DeleteWorkScheduleOutcome.NotFound => TypedResults.NotFound(),
            DeleteWorkScheduleOutcome.InUse => Conflict(InUseDetail),
            _ => throw new InvalidOperationException("Resultado del borrado no contemplado."),
        };

    private static IResult Failure(SaveWorkScheduleResult result) => result switch
    {
        SaveWorkScheduleResult.Invalid invalid => TypedResults.ValidationProblem(
            invalid.Errors.GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()),
            title: "Solicitud no válida"),
        SaveWorkScheduleResult.DuplicateName => Conflict(DuplicateNameDetail),
        SaveWorkScheduleResult.CapReached => Conflict(CapReachedDetail),
        SaveWorkScheduleResult.NotFound => TypedResults.NotFound(),
        _ => throw new InvalidOperationException("Resultado de la jornada no contemplado."),
    };

    private static ProblemHttpResult Conflict(string detail)
        => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: detail);

    private static ProblemHttpResult BadRequest()
        => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Solicitud no válida", detail: "El cuerpo de la solicitud es obligatorio.");
}
