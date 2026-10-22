namespace TimeTracker.Api.Authentication;

/// <summary>
/// Los cinco datos del registro y nada más (asignación masiva, API3): las propiedades desconocidas del cuerpo (<c>role</c>,
/// <c>companyId</c>, <c>usesPlatformAssistant</c>) se ignoran y no tienen efecto.
/// </summary>
public sealed record RegisterCompanyRequest(string? CompanyName, string? ManagerName, string? Email, string? Password, string? InviteCode);
