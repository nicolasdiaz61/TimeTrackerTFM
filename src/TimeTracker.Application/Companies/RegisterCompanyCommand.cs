namespace TimeTracker.Application.Companies;

/// <summary>Los cinco datos del cuerpo del registro. Nada más del cuerpo se enlaza (asignación masiva, API3).</summary>
public sealed record RegisterCompanyCommand(string? CompanyName, string? ManagerName, string? Email, string? Password, string? InviteCode);
