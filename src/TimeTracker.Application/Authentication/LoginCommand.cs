namespace TimeTracker.Application.Authentication;

/// <summary>Credenciales recibidas tal cual: pueden faltar o venir mal formadas; el caso de uso las comprueba.</summary>
public sealed record LoginCommand(string? Email, string? Password);
