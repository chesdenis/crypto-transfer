using Microsoft.Extensions.Logging;

namespace ct.point;

public class CtSpectreConsoleLoggerProvider(Action<LogLevel, string, string> logSink) : ILoggerProvider
{
    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CtSpectreConsoleLogger(categoryName, logSink);
    }

    private class CtSpectreConsoleLogger(string categoryName, Action<LogLevel, string, string> logSink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            logSink(logLevel, categoryName, message);
        }
    }
}