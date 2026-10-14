using TimeTracker.Application.Team;

namespace TimeTracker.Api.Team;

/// <summary>Mínima exposición: solo identificador y nombre (sin correo, rol ni responsable).</summary>
public sealed record TeamMemberResponse(Guid Id, string DisplayName)
{
    public static TeamMemberResponse From(TeamMemberResult result) => new(result.Id, result.DisplayName);
}

public sealed record TeamMemberListResponse(IReadOnlyList<TeamMemberResponse> Items)
{
    public static TeamMemberListResponse From(IReadOnlyList<TeamMemberResult> members)
        => new([.. members.Select(TeamMemberResponse.From)]);
}
