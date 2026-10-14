using System.Security.Claims;

using TimeTracker.Api.Security;
using TimeTracker.Application.Authentication;

namespace TimeTracker.Api.Authentication;

public static partial class AuthEndpoints
{
    private const string LoggerCategory = "TimeTracker.Api.Authentication.AuthEndpoints";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.LoginPolicy)
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Sin base de datos: todo sale de los claims del token ya validado (AU-33). Lo protege la política de respaldo.
        group.MapGet("me", (ClaimsPrincipal user) => TypedResults.Ok(new UserResponse(
                Guid.Parse(user.FindFirstValue("sub")!),
                user.FindFirstValue("email")!,
                user.FindFirstValue("name")!,
                user.FindFirstValue("role")!)))
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest? request,
        LoginUseCase useCase,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Solicitud no válida",
                detail: "El cuerpo de la solicitud es obligatorio.");
        }

        var result = await useCase.ExecuteAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
        if (result is null)
        {
            // Sin correo, contraseña ni IP en el registro (A09): solo el hecho del intento fallido.
            LogFailedLogin(loggerFactory.CreateLogger(LoggerCategory));
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Credenciales no válidas",
                detail: "El correo electrónico o la contraseña no son correctos.");
        }

        return TypedResults.Ok(LoginResponse.From(result));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Intento de inicio de sesión rechazado.")]
    private static partial void LogFailedLogin(ILogger logger);
}
