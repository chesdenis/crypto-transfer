using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace ct.lib.extensions;

public static class CtLoggingExtensions
{
    public static string FormatLogMessage(LogLevel logLevel, string category, string message)
    {
        string timestamp = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

        string logLevelText = logLevel switch
        {
            LogLevel.Information => $"[blue]{logLevel}[/]",
            LogLevel.Warning => $"[yellow]{logLevel}[/]",
            LogLevel.Error => $"[red]{logLevel}[/]",
            LogLevel.Critical => $"[bold red]{logLevel}[/]",
            LogLevel.Debug => $"[green italic]{logLevel}[/]",
            LogLevel.Trace => $"[grey italic]{logLevel}[/]",
            _ => logLevel.ToString()
        };

        return $"{timestamp} {logLevelText} [grey]in {category.EscapeMarkup()}[/]: {message.EscapeMarkup()}";
    }
}