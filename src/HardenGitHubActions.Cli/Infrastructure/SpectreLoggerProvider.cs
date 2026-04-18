using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace HardenGitHubActions.Cli.Infrastructure;

internal sealed class SpectreLoggerProvider(IAnsiConsole console, LogLevel minLevel) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SpectreLogger(console, minLevel);

    public void Dispose() { }

    private sealed class SpectreLogger(IAnsiConsole console, LogLevel minLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel)) return;

            var message = Markup.Escape(formatter(state, exception));
            var (tag, colour) = logLevel switch
            {
                LogLevel.Trace       => ("trace", "grey"),
                LogLevel.Debug       => ("debug", "grey"),
                LogLevel.Information => ("info ", "blue"),
                LogLevel.Warning     => ("warn ", "yellow"),
                LogLevel.Error       => ("error", "red"),
                LogLevel.Critical    => ("crit ", "red bold"),
                _                    => ("?    ", "white"),
            };

            console.MarkupLine($"[{colour}]{tag}[/] {message}");
            if (exception is not null)
            {
                console.WriteException(exception);
            }
        }
    }
}
