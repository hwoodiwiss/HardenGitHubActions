using System.CommandLine;
using HardenGitHubActions.Cli;
using HardenGitHubActions.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace HardenGitHubActions.Tests.Cli;

public sealed class HardenCommandTests : IDisposable
{
    private readonly string _root;
    private readonly FakeGitHubApiClient _fakeClient;

    public HardenCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"hga-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, ".github", "workflows"));
        _fakeClient = new FakeGitHubApiClient();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Test 1 — no args → RepositoryRoot defaults to ".", CommentMode defaults to MostSpecificTag, Token null.
    [Test]
    public async Task Parse_NoArgs_DefaultSettingsParsed()
    {
        var rootCommand = BuildRootCommand();

        var parseResult = rootCommand.Parse([]);

        using (Assert.Multiple())
        {
            await Assert.That(parseResult.Errors.Count).IsEqualTo(0);
            await Assert.That(parseResult.GetRequiredValue<string>(HardenCommand.Inputs.RepositoryRoot)).IsEqualTo(".");
            await Assert.That(parseResult.GetRequiredValue(HardenCommand.Inputs.CommentMode)).IsEqualTo(TagCommentMode.MostSpecificTag);
            await Assert.That(parseResult.GetRequiredValue(HardenCommand.Inputs.GitHubToken)).IsNull();
        }
    }

    // Test 2 — --comment-mode ExactTag is parsed.
    [Test]
    public async Task Parse_CommentModeExactTag_Parsed()
    {
        var rootCommand = BuildRootCommand();

        var parseResult = rootCommand.Parse(["--comment-mode", "ExactTag"]);

        using (Assert.Multiple())
        {
            await Assert.That(parseResult.Errors.Count).IsEqualTo(0);
            await Assert.That(parseResult.GetRequiredValue(HardenCommand.Inputs.CommentMode)).IsEqualTo(TagCommentMode.ExactTag);
        }
    }

    // Test 3 — --token is parsed and forwarded to the factory on Invoke.
    [Test]
    public async Task Invoke_TokenArg_TokenForwardedToFactory()
    {
        string? capturedToken = null;
        var rootCommand = BuildRootCommand(hardenerFactory: (token, _) =>
        {
            capturedToken = token;
            return new WorkflowHardener(_fakeClient);
        });

        var exit = await rootCommand.Parse(["--token", "my-pat", _root]).InvokeAsync();

        using (Assert.Multiple())
        {
            await Assert.That(exit).IsEqualTo(0);
            await Assert.That(capturedToken).IsEqualTo("my-pat");
        }
    }

    // Test 4 — successful run (no workflow files) returns exit code 0.
    [Test]
    public async Task Invoke_SuccessfulRun_ReturnsExitCodeZero()
    {
        var rootCommand = BuildRootCommand();

        var exit = await rootCommand.Parse([_root]).InvokeAsync();

        await Assert.That(exit).IsEqualTo(0);
    }

    // Test 5 — GitHubApiException → exit code 1 and error message in output.
    [Test]
    public async Task Invoke_GitHubApiException_ReturnsExitCodeOneWithMessage()
    {
        var workflowPath = Path.Combine(_root, ".github", "workflows", "ci.yml");
        await File.WriteAllTextAsync(workflowPath, "      - uses: actions/checkout@v4");

        var (rootCommand, output) = BuildRootCommandWithCapture();

        var exit = await rootCommand.Parse([_root]).InvokeAsync();

        using (Assert.Multiple())
        {
            await Assert.That(exit).IsEqualTo(1);
            await Assert.That(output.ToString()).Contains("GitHub API error");
        }
    }

    // Test 6 — --verbose flag is parsed.
    [Test]
    public async Task Parse_VerboseFlag_Parsed()
    {
        var rootCommand = BuildRootCommand();

        var parseResult = rootCommand.Parse(["--verbose"]);

        using (Assert.Multiple())
        {
            await Assert.That(parseResult.Errors.Count).IsEqualTo(0);
            await Assert.That(parseResult.GetRequiredValue(HardenCommand.Inputs.Verbose)).IsTrue();
        }
    }

    // Test 7 — --quiet flag is parsed.
    [Test]
    public async Task Parse_QuietFlag_Parsed()
    {
        var rootCommand = BuildRootCommand();

        var parseResult = rootCommand.Parse(["--quiet"]);

        using (Assert.Multiple())
        {
            await Assert.That(parseResult.Errors.Count).IsEqualTo(0);
            await Assert.That(parseResult.GetRequiredValue(HardenCommand.Inputs.Quiet)).IsTrue();
        }
    }

    // Test 8 — --dry-run flag is forwarded into HardeningOptions; file is not modified.
    [Test]
    public async Task Invoke_DryRunFlag_FileNotModified()
    {
        _fakeClient.SetupResolve("actions", "checkout", "v4", "aabbccddaabbccddaabbccddaabbccddaabbccdd");
        var workflowPath = Path.Combine(_root, ".github", "workflows", "ci.yml");
        const string original = "      - uses: actions/checkout@v4";
        await File.WriteAllTextAsync(workflowPath, original);

        var rootCommand = BuildRootCommand();
        var exit = await rootCommand.Parse(["--dry-run", _root]).InvokeAsync();

        var actual = await File.ReadAllTextAsync(workflowPath);
        using (Assert.Multiple())
        {
            await Assert.That(exit).IsEqualTo(0);
            await Assert.That(actual).IsEqualTo(original);
        }
    }

    // Test 9 — --verbose passes LogLevel.Debug to the factory.
    [Test]
    public async Task Invoke_VerboseFlag_PassesDebugLogLevelToFactory()
    {
        LogLevel? capturedLevel = null;
        var rootCommand = BuildRootCommand(hardenerFactory: (_, level) =>
        {
            capturedLevel = level;
            return new WorkflowHardener(_fakeClient);
        });

        await rootCommand.Parse(["--verbose", _root]).InvokeAsync();

        await Assert.That(capturedLevel).IsEqualTo(LogLevel.Debug);
    }

    // Test 10 — --quiet passes LogLevel.Warning to the factory.
    [Test]
    public async Task Invoke_QuietFlag_PassesWarningLogLevelToFactory()
    {
        LogLevel? capturedLevel = null;
        var rootCommand = BuildRootCommand(hardenerFactory: (_, level) =>
        {
            capturedLevel = level;
            return new WorkflowHardener(_fakeClient);
        });

        await rootCommand.Parse(["--quiet", _root]).InvokeAsync();

        await Assert.That(capturedLevel).IsEqualTo(LogLevel.Warning);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private RootCommand BuildRootCommand(Func<string?, LogLevel, WorkflowHardener>? hardenerFactory = null)
        => BuildRootCommandWithCapture(hardenerFactory).RootCommand;

    private (RootCommand RootCommand, StringWriter Output) BuildRootCommandWithCapture(
        Func<string?, LogLevel, WorkflowHardener>? hardenerFactory = null)
    {
        hardenerFactory ??= (_, _) => new WorkflowHardener(_fakeClient);

        var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });

        var services = new ServiceCollection();
        services.AddSingleton(console);
        services.AddSingleton(hardenerFactory);
        services.AddSingleton<HardenCommand>();
        var sp = services.BuildServiceProvider();

        var rootCommand = HardenCommand.BuildRootCommand(sp);
        return (rootCommand, writer);
    }
}
