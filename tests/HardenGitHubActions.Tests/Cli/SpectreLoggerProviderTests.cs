using System.Globalization;
using HardenGitHubActions.Cli.Infrastructure;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace HardenGitHubActions.Tests.Cli;

public sealed class SpectreLoggerProviderTests
{
    // Test 1 — the Spectre writer renders the exception type, message and a
    // stack frame for the throwing method.
    [Test]
    public async Task WriteExceptionWithSpectre_RendersTypeMessageAndStack()
    {
        var (console, output) = CreateConsole();
        var ex = MakeException();

        SpectreLoggerProvider.WriteExceptionWithSpectre(console, ex);

        var text = output.ToString();
        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("InvalidOperationException");
            await Assert.That(text).Contains("kapow");
            await Assert.That(text).Contains(nameof(MakeException));
        }
    }

    // Test 2 — the AOT-safe fallback emits a "Type: Message" header and the
    // stack trace.
    [Test]
    public async Task WriteExceptionFallback_EmitsTypeMessageAndStack()
    {
        var (console, output) = CreateConsole();
        var ex = MakeException();

        SpectreLoggerProvider.WriteExceptionFallback(console, ex);

        var text = output.ToString();
        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("System.InvalidOperationException: kapow");
            await Assert.That(text).Contains(nameof(MakeException));
        }
    }

    // Test 3 — the fallback walks the InnerException chain so root causes are
    // visible.
    [Test]
    public async Task WriteExceptionFallback_IncludesInnerException()
    {
        var (console, output) = CreateConsole();
        var inner = new ArgumentException("inner-cause");
        var outer = new InvalidOperationException("outer", inner);

        SpectreLoggerProvider.WriteExceptionFallback(console, outer);

        var text = output.ToString();
        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("InvalidOperationException");
            await Assert.That(text).Contains("ArgumentException");
            await Assert.That(text).Contains("inner-cause");
        }
    }

    // Test 4 — going through the full logger pipeline still surfaces the
    // exception (regression: don't silently swallow). Routes via whichever
    // branch the current runtime supports.
    [Test]
    public async Task Log_WithException_RendersException()
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
