namespace TimeTracker.Application.Team;

/// <summary>Mínima exposición (DD-16): solo identificador y nombre; sin correo.</summary>
public sealed record TeamMemberResult(Guid Id, string DisplayName, Guid? WorkScheduleId = null);
