namespace TimeTracker.Web.Team;

// DTO duplicado a propósito (sin referencias a src/). La API solo expone identificador y nombre visible.
public sealed record TeamMemberDto(Guid Id, string DisplayName);

public sealed record TeamMemberListDto(IReadOnlyList<TeamMemberDto> Items);
