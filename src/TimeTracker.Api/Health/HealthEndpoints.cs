namespace TimeTracker.Api.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Anónimo a propósito: lo usan el HEALTHCHECK de Docker y los balanceadores, que no tienen token.
        endpoints.MapGet("/health", () => TypedResults.Ok(new HealthResponse("Healthy"))).AllowAnonymous();
        return endpoints;
    }
}
