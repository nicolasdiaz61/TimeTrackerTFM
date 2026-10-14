using Microsoft.AspNetCore.Http.HttpResults;

using TimeTracker.Api.TimeEntries;
using TimeTracker.Application.Team;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Team;

public static class TeamEndpoints
{
    public const string ManagerPolicy = "Manager";

    public static IServiceCollection AddTeam(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(ManagerPolicy, policy => policy.RequireRole(nameof(UserRole.Manager)));
        services.AddScoped<GetTeamMembersUseCase>();
        services.AddScoped<GetTeamMemberTimeEntriesUseCase>();

        return services;
    }

    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // El rol se exige en el grupo: un empleado recibe 403 antes de que se mire a qué miembro se refiere la ruta.
        var group = endpoints.MapGroup("/api/team").WithTags("Team").RequireAuthorization(ManagerPolicy)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("members", async (GetTeamMembersUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Ok(TeamMemberListResponse.From(await useCase.ExecuteAsync(cancellationToken))));

        // El id solo se enlaza si es un GUID: otro texto no casa con la ruta y da el mismo 404 que un miembro ajeno.
        group.MapGet("members/{memberId:guid}/time-entries", MemberTimeEntriesAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    // Ajeno, inexistente o el propio responsable: el caso de uso devuelve null y el 404 es idéntico en los tres (DD-15).
    private static async Task<Results<Ok<TimeEntryListResponse>, NotFound>> MemberTimeEntriesAsync(
        Guid memberId,
        GetTeamMemberTimeEntriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(memberId, cancellationToken);

        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(TimeEntryListResponse.From(result));
    }
}
