namespace TimeTracker.Application.Team;

/// <summary>
/// Datos del alta de una persona. No lleva compañía ni contraseña: la compañía sale del token y la contraseña temporal la genera el
/// servidor. <paramref name="Role"/> nulo equivale a <c>Employee</c>; <paramref name="ManagerId"/> nulo, al Manager que crea.
/// </summary>
public sealed record CreateTeamMemberCommand(string? DisplayName, string? Email, string? Role, Guid? ManagerId);
