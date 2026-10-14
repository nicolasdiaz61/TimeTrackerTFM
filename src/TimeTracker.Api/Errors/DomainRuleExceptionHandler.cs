using Microsoft.AspNetCore.Diagnostics;

using TimeTracker.Domain;

namespace TimeTracker.Api.Errors;

/// <summary>Traduce cualquier infracción de una regla de negocio en un 409 con mensaje en español (DD-7).</summary>
public sealed partial class DomainRuleExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<DomainRuleExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainRuleException rule)
        {
            return false;
        }

        // Sin el identificador del empleado: los registros no deben llevar datos personales.
        LogRuleViolation(logger, rule.GetType().Name);
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = StatusCodes.Status409Conflict, Title = rule.Title, Detail = rule.Message },
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Regla de negocio incumplida: {RuleType}")]
    private static partial void LogRuleViolation(ILogger logger, string ruleType);
}
