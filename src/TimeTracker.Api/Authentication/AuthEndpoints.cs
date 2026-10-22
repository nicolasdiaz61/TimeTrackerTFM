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

        // Sin base de datos: todo sale de los claims del token ya validado (AU-33), incluidos cname y mcp (DD-6). Lo protege la política de respaldo.
        group.MapGet("me", (ClaimsPrincipal user) => TypedResults.Ok(new UserResponse(
                Guid.Parse(user.FindFirstValue("sub")!),
                user.FindFirstValue("email")!,
                user.FindFirstValue("name")!,
                user.FindFirstValue("role")!,
                user.FindFirstValue(JwtDefaults.CompanyNameClaim) ?? string.Empty,
                user.FindFirstValue(JwtDefaults.PasswordChangePendingClaim) == JwtDefaults.PasswordChangePendingValue)))
            .AllowWhilePasswordChangePending()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Autenticado (política de respaldo) y permitido con el cambio de contraseña pendiente: es la salida del bloqueo del claim mcp.
        group.MapPost("change-password", ChangePasswordAsync)
            .AllowWhilePasswordChangePending()
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.ChangePasswordPolicy)
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest? request,
        ChangePasswordUseCase useCase,
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

        var logger = loggerFactory.CreateLogger(LoggerCategory);
        switch (await useCase.ExecuteAsync(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken))
        {
            case ChangePasswordResult.Changed changed:
                LogPasswordChanged(logger);
                return TypedResults.Ok(LoginResponse.From(changed.Session));
            case ChangePasswordResult.Invalid invalid:
                // Solo el hecho y los campos afectados: ninguna contraseña, ni la actual ni la nueva, llega al registro (A09).
                LogPasswordChangeRejected(logger);
                return TypedResults.ValidationProblem(
                    invalid.Errors.GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()),
                    title: "Solicitud no válida");
            case ChangePasswordResult.Conflict:
                LogPasswordChangeRejected(logger);
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflicto",
                    detail: "Su cuenta ha cambiado mientras se procesaba el cambio de contraseña. Vuelva a intentarlo.");
            case ChangePasswordResult.Unauthorized:
                LogPasswordChangeRejected(logger);
                return InvalidCredentials();
            default:
                throw new InvalidOperationException("Resultado del cambio de contraseña no contemplado.");
        }
    }

    // El mismo 401 genérico del inicio de sesión: una compañía suspendida no se distingue de unas credenciales erróneas.
    private static IResult InvalidCredentials() => TypedResults.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Credenciales no válidas",
        detail: "El correo electrónico o la contraseña no son correctos.");

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
            return InvalidCredentials();
        }

        return TypedResults.Ok(LoginResponse.From(result));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Intento de inicio de sesión rechazado.")]
    private static partial void LogFailedLogin(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cambio de contraseña realizado.")]
    private static partial void LogPasswordChanged(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cambio de contraseña rechazado.")]
    private static partial void LogPasswordChangeRejected(ILogger logger);
}
