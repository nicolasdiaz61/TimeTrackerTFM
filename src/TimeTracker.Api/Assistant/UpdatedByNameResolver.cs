using TimeTracker.Application.Users;

namespace TimeTracker.Api.Assistant;

/// <summary>
/// Nombre visible de quien guardó la configuración. Es un dato de cortesía: si la consulta falla (incluso tras confirmarse
/// el guardado) se responde sin nombre en lugar de un 500 que haría creer que el cambio no se aplicó.
/// </summary>
public sealed partial class UpdatedByNameResolver(IUserRepository users, ILogger<UpdatedByNameResolver> logger)
{
    public async Task<string?> ResolveAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return null;
        }

        try
        {
            return await users.FindDisplayNameAsync(id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogLookupFailed(logger, exception.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo consultar el nombre de quien guardó la configuración del asistente ({ExceptionType}); se responde sin nombre.")]
    private static partial void LogLookupFailed(ILogger logger, string exceptionType);
}
