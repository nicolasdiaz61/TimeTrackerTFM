namespace TimeTracker.Web.Auth;

// DTO duplicado a propósito: la Web no referencia proyectos de src/. Solo se usa el token; el resto lo da el propio token.
public sealed record LoginResponseDto(string AccessToken, DateTime ExpiresAt);
