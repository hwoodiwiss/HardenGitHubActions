using System.Globalization;
using HardenGitHubActions.Cli.Infrastructure;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace HardenGitHubActions.Tests.Cli;

public sealed class SpectreLoggerProviderTests
{
    // Test 1 — when dynamic code IS supported, an exception is rendered with
    // Spectre's WriteException (which produces its rich, multi-segment output).
    [Test]
    public async Task Log_DynamicCodeSupported_UsesSpectreWriteException()
    {
        var (console, output) = CreateConsole();
        var ex = MakeException();
        var provider = new SpectreLoggerProvider(console, LogLevel.Trace, dynamicCodeSupported: true);
        var logger = provider.CreateLogger("cat");

        logger.Log(LogLevel.Error, new EventId(0), "boom", ex, static (s, _) => s);

        // Spectre's WriteException produces the type name AND the message AND
        // the standard "at <method>" stack-frame prefix.
        var text = output.ToString();
        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("InvalidOperationException");
            await Assert.That(text).Contains("kapow");
            await Assert.That(text).Contains(nameof(MakeException));
        }
    }

    // Test 2 — when dynamic code is NOT supported (AOT), the fallback writer
    // emits a "Type: Message" header and the stack trace, without calling
    // WriteException.
    [Test]
    public async Task Log_DynamicCodeNotSupported_FallbackEmitsTypeMessageAndStack()
    {
        var (console, output) = CreateConsole();
        var ex = MakeException();
        var provider = new SpectreLoggerProvider(console, LogLevel.Trace, dynamicCodeSupported: false);
        var logger = provider.CreateLogger("cat");

        logger.Log(LogLevel.Error, new EventId(0), "boom", ex, static (s, _) => s);

        var text = output.ToString();
        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("System.InvalidOperationException: kapow");
            await Assert.That(text).Contains(nameof(MakeException));
        }
    }

    // Test 3 — fallback walks the InnerException chain so root causes are visible.
    [Test]
    public async Task Log_DynamicCodeNotSupported_FallbackIncludesInnerException()
    {
        var (console, output) = CreateConsole();
        var inner = new ArgumentException("inner-cause");
        var outer = new InvalidOperationException("outer", inner);
        var provider = new SpectreLoggerProvider(console, LogLevel.Trace, dynamicCodeSupported: false);
        var logger = provider.CreateLogger("cat");

        logger.Log(LogLevel.Error, new EventId(0), "boom", outer, static (s, _) => s);

        var text = output.ToString();
        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("InvalidOperationException");
            await Assert.That(text).Contains("ArgumentException");
            await Assert.That(text).Contains("inner-cause");
        }
    }

    // Test 4 — default constructor wires up to the real RuntimeFeature flag and
    // still produces *some* exception output (regression: don't silently swallow).
    [Test]
    public async Task Log_DefaultConstructor_RendersException()
    {
        var (console, output) = CreateConsole();
        var ex = MakeException();
        var provider = new SpectreLoggerProvider(console, LogLevel.Trace);
        var logger = provider.CreateLogger("cat");

        logger.Log(LogLevel.Error, new EventId(0), "boom", ex, static (s, _) => s);

        await Assert.That(output.ToString()).Contains("kapow");
    }

    private static (IAnsiConsole console, StringWriter output) CreateConsole()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });
        return (console, writer);
    }

    private static InvalidOperationException MakeException()
    {
        try
        {
            throw new InvalidOperationException("kapow");
        }
        catch (InvalidOperationException caught)
        {
            return caught;
        }
    }
}
