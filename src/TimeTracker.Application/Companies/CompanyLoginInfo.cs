using TimeTracker.Domain.Companies;

namespace TimeTracker.Application.Companies;

/// <summary>Lo que el inicio de sesión necesita de la compañía de un usuario: su nombre visible y si puede iniciar sesión.</summary>
public sealed record CompanyLoginInfo(string Name, CompanyStatus Status, bool UsesPlatformAssistant);
