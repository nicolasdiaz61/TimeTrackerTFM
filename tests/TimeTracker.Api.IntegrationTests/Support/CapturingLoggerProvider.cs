using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>Captura todos los mensajes (y sus excepciones) para comprobar que un secreto no llega al registro.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => messages.Enqueue($"{formatter(state, exception)} {exception}");
    }
}
