using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using TimeTracker.Api.Security;
using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Users;

namespace TimeTracker.Api.Assistant;

public static class AssistantConfigurationEndpoints
{
    public const string Route = "/api/asistente/configuracion";

    // Los campos caben de sobra en 8 KiB; el resto se rechaza con 413 sin leerlo (DD-14).
    public const long MaxRequestBodyBytes = 8 * 1024;

    private const string ConflictDetail = "La configuración cambió desde que la consultó. Recárguela e inténtelo de nuevo.";

    public static IServiceCollection AddAssistantConfiguration(this IServiceCollection services)
    {
        services.AddScoped<GetAssistantSettingsUseCase>();
        services.AddScoped<SaveAssistantSettingsUseCase>();
        services.AddScoped<ResetAssistantSettingsUseCase>();
        services.AddScoped<TestAssistantConnectionUseCase>();

        return services;
    }

    public static IEndpointRouteBuilder MapAssistantConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // El rol se exige en el grupo, antes de cualquier lectura o escritura; el limitador de la prueba va después de autorizar (DD-15).
        var group = endpoints.MapGroup(Route).WithTags("AssistantConfiguration").RequireAuthorization(AuthorizationPolicies.Manager)
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodyBytes))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet(string.Empty, GetAsync);

        group.MapPut(string.Empty, SaveAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete(string.Empty, ResetAsync)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("prueba", TestAsync)
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.AssistantConfigTestPolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    private static async Task<Ok<AssistantConfigurationResponse>> GetAsync(
        GetAssistantSettingsUseCase useCase, IUserRepository users, CancellationToken cancellationToken)
        => TypedResults.Ok(await ToResponseAsync(await useCase.ExecuteAsync(cancellationToken), users, cancellationToken));

    private static async Task<Results<Ok<AssistantConfigurationResponse>, ValidationProblem, ProblemHttpResult>> SaveAsync(
        AssistantConfigurationRequest? request,
        SaveAssistantSettingsUseCase useCase,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BodyRequired();
        }

        var input = new AssistantSettingsInput(request.Provider, request.Model, request.TimeoutSeconds, request.MaxOutputTokens, request.Effort, request.Credential);
        switch (await useCase.ExecuteAsync(new SaveAssistantSettingsCommand(input, request.ConcurrencyToken), cancellationToken))
        {
            case SaveAssistantSettingsResult.Saved saved:
                return TypedResults.Ok(await ToResponseAsync(saved.View, users, cancellationToken));
            case SaveAssistantSettingsResult.Invalid invalid:
                return Invalid(invalid.Errors);
            default:
                return Conflict();
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetAsync(ResetAssistantSettingsUseCase useCase, CancellationToken cancellationToken)
        => await useCase.ExecuteAsync(cancellationToken) == DeleteOutcome.Conflict ? Conflict() : TypedResults.NoContent();

    private static async Task<Results<Ok<AssistantConnectionTestResponse>, ValidationProblem>> TestAsync(
        AssistantConnectionTestRequest? request, TestAssistantConnectionUseCase useCase, CancellationToken cancellationToken)
    {
        // Sin cuerpo se prueba la configuración efectiva (E-6).
        request ??= new AssistantConnectionTestRequest(null, null, null, null, null, null);
        var input = new AssistantSettingsInput(request.Provider, request.Model, request.TimeoutSeconds, request.MaxOutputTokens, request.Effort, request.Credential);

        return await useCase.ExecuteAsync(input, cancellationToken) switch
        {
            TestAssistantConnectionResult.Completed completed => TypedResults.Ok(new AssistantConnectionTestResponse(completed.Outcome.Result, completed.Outcome.Message)),
            TestAssistantConnectionResult.Invalid invalid => Invalid(invalid.Errors),
            _ => throw new InvalidOperationException("Resultado de la prueba de conexión no contemplado."),
        };
    }

    private static async Task<AssistantConfigurationResponse> ToResponseAsync(AssistantSettingsView view, IUserRepository users, CancellationToken cancellationToken)
        => AssistantConfigurationResponse.From(view, view.UpdatedBy is { } id ? await users.FindDisplayNameAsync(id, cancellationToken) : null);

    private static ValidationProblem Invalid(IReadOnlyList<FieldError> errors) => TypedResults.ValidationProblem(
        errors.GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()),
        title: "Solicitud no válida");

    private static ProblemHttpResult Conflict()
        => TypedResults.Problem(detail: ConflictDetail, statusCode: StatusCodes.Status409Conflict, title: "Conflicto");

    private static ProblemHttpResult BodyRequired()
        => TypedResults.Problem(detail: "El cuerpo de la solicitud es obligatorio.", statusCode: StatusCodes.Status400BadRequest, title: "Solicitud no válida");
}
