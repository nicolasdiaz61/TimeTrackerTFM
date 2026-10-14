using Microsoft.Extensions.Options;

namespace TimeTracker.Api.IntegrationTests;

/// <summary>Recorre la excepción de arranque, incluidos <c>InnerException</c> y <c>AggregateException</c> (V9).</summary>
internal static class StartupFailure
{
    public static IEnumerable<Exception> Flatten(Exception? exception)
    {
        if (exception is null)
        {
            yield break;
        }

        yield return exception;

        IEnumerable<Exception> children = exception is AggregateException aggregate
            ? aggregate.InnerExceptions
            : exception.InnerException is null ? [] : [exception.InnerException];
        foreach (var child in children.SelectMany(Flatten))
        {
            yield return child;
        }
    }

    /// <summary>Todas las excepciones de validación de opciones encontradas en el árbol.</summary>
    public static IReadOnlyList<OptionsValidationException> Validations(Exception? exception)
        => [.. Flatten(exception).OfType<OptionsValidationException>()];

    /// <summary>Texto de todos los mensajes del árbol, para comprobar que un valor no se imprime.</summary>
    public static string AllMessages(Exception? exception)
        => string.Join(Environment.NewLine, Flatten(exception).Select(inner => inner.Message));
}
