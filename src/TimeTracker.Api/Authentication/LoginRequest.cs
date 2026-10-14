namespace TimeTracker.Api.Authentication;

// Solo dos campos: nada más del cuerpo se enlaza (asignación masiva, API3).
public sealed record LoginRequest(string? Email, string? Password);
