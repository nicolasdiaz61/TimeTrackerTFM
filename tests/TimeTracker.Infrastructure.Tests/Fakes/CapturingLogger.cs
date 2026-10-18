using Microsoft.Extensions.Logging;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>Registrador que guarda los mensajes ya formateados para comprobar qué datos llegan (o no) al registro.</summary>
internal sealed class CapturingLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
