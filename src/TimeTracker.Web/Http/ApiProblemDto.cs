namespace TimeTracker.Web.Http;

// Subconjunto de ProblemDetails (RFC 9457) que la pantalla necesita.
public sealed record ApiProblemDto(string? Title, string? Detail);
