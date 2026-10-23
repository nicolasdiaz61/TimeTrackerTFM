namespace TimeTracker.Web.Team;

// DTO duplicado a propósito (sin referencias a src/). La API solo expone identificador, nombre visible y jornada asignada.
public sealed record TeamMemberDto(Guid Id, string DisplayName, Guid? WorkScheduleId = null);

public sealed record TeamMemberListDto(IReadOnlyList<TeamMemberDto> Items);
