using TimeTracker.Application.Team;

namespace TimeTracker.Api.Team;

/// <summary>Mínima exposición: identificador, nombre y jornada asignada, si la tiene (sin correo, rol ni responsable).</summary>
public sealed record TeamMemberResponse(Guid Id, string DisplayName, Guid? WorkScheduleId)
{
    public static TeamMemberResponse From(TeamMemberResult result) => new(result.Id, result.DisplayName, result.WorkScheduleId);
}

public sealed record TeamMemberListResponse(IReadOnlyList<TeamMemberResponse> Items)
{
    public static TeamMemberListResponse From(IReadOnlyList<TeamMemberResult> members)
        => new([.. members.Select(TeamMemberResponse.From)]);
}
