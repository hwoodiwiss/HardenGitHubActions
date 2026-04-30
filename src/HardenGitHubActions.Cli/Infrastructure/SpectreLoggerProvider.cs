using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace HardenGitHubActions.Cli.Infrastructure;

internal sealed class SpectreLoggerProvider(IAnsiConsole console, LogLevel minLevel, bool dynamicCodeSupported) : ILoggerProvider
{
    public SpectreLoggerProvider(IAnsiConsole console, LogLevel minLevel)
        : this(console, minLevel, RuntimeFeature.IsDynamicCodeSupported)
    {
    }

    public ILogger CreateLogger(string categoryName) => new SpectreLogger(console, minLevel, dynamicCodeSupported);

    public void Dispose() { }

    private sealed class SpectreLogger(IAnsiConsole console, LogLevel minLevel, bool dynamicCodeSupported) : ILogger
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
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = Markup.Escape(formatter(state, exception));
            var (tag, colour) = logLevel switch
            {
                LogLevel.Trace => ("trace", "grey"),
                LogLevel.Debug => ("debug", "grey"),
                LogLevel.Information => ("info ", "blue"),
                LogLevel.Warning => ("warn ", "yellow"),
                LogLevel.Error => ("error", "red"),
                LogLevel.Critical => ("crit ", "red bold"),
                _ => ("?    ", "white"),
            };

            console.MarkupLine($"[{colour}]{tag}[/] {message}");
            if (exception is not null)
            {
                WriteException(console, exception, dynamicCodeSupported);
            }
        }

        // Spectre's IAnsiConsole.WriteException uses reflection-heavy formatting
        // that is annotated [RequiresDynamicCode] and is not safe under NativeAOT.
        // When dynamic code is unavailable we fall back to a string-only writer.
        private static void WriteException(IAnsiConsole console, Exception exception, bool dynamicCodeSupported)
        {
            if (dynamicCodeSupported)
            {
                WriteExceptionWithSpectre(console, exception);
            }
            else
            {
                WriteExceptionFallback(console, exception);
            }
        }

        // Isolated in its own method so the [RequiresDynamicCode] suppression is
        // as narrow as possible and the AOT analyser only ever sees this single
        // call site, gated by the runtime check above.
        [UnconditionalSuppressMessage(
            "AOT",
            "IL3050:RequiresDynamicCode",
            Justification = "Caller gates this call on RuntimeFeature.IsDynamicCodeSupported; the AOT-safe path is WriteExceptionFallback.")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteExceptionWithSpectre(IAnsiConsole console, Exception exception)
            => console.WriteException(exception);

        private static void WriteExceptionFallback(IAnsiConsole console, Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                var header = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1}",
                    current.GetType().FullName,
                    current.Message);
                console.MarkupLine($"[red]{Markup.Escape(header)}[/]");

                if (!string.IsNullOrEmpty(current.StackTrace))
                {
                    console.WriteLine(current.StackTrace);
                }

                if (current.InnerException is not null)
                {
                    console.MarkupLine("[red] ---> (inner exception)[/]");
                }
            }
        }
    }
}
