using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using TimeTracker.Api.Security;
using TimeTracker.Api.TimeEntries;
using TimeTracker.Api.WorkSchedules;
using TimeTracker.Application.Team;

namespace TimeTracker.Api.Team;

public static class TeamEndpoints
{
    public static IServiceCollection AddTeam(this IServiceCollection services)
    {
        services.AddScoped<GetTeamMembersUseCase>();
        services.AddScoped<GetTeamMemberTimeEntriesUseCase>();
        services.AddScoped<GetTeamSummaryUseCase>();

        return services;
    }

    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // El rol se exige en el grupo: un empleado recibe 403 antes de que se mire a qué miembro se refiere la ruta.
        var group = endpoints.MapGroup("/api/team").WithTags("Team").RequireAuthorization(AuthorizationPolicies.Manager)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("members", async (GetTeamMembersUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Ok(TeamMemberListResponse.From(await useCase.ExecuteAsync(cancellationToken))));

        // El id solo se enlaza si es un GUID: otro texto no casa con la ruta y da el mismo 404 que un miembro ajeno.
        group.MapGet("members/{memberId:guid}/time-entries", MemberTimeEntriesAsync)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Asigna o retira la jornada de un miembro del equipo: un miembro ajeno o una jornada ajena dan el mismo 404.
        group.MapPut("members/{memberId:guid}/work-schedule", WorkScheduleEndpoints.AssignAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    // Ajeno, inexistente o el propio responsable: el caso de uso devuelve null y el 404 es idéntico en los tres (DD-15).
    private static async Task<Results<Ok<TimeEntryListResponse>, NotFound, ProblemHttpResult>> MemberTimeEntriesAsync(
        Guid memberId,
        [FromQuery(Name = "from")] string? from,
        [FromQuery(Name = "to")] string? to,
        TimeProvider clock,
        GetTeamMemberTimeEntriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        // La ventana se valida antes de buscar al miembro: el 400 no revela si el miembro es del equipo.
        if (TimeWindowQuery.TryRead(from, to, clock.GetUtcNow(), out var window) is { } error)
        {
            return TimeEntryEndpoints.InvalidWindow(error);
        }

        var result = await useCase.ExecuteAsync(memberId, window, cancellationToken);

        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(TimeEntryListResponse.From(result));
    }
}
