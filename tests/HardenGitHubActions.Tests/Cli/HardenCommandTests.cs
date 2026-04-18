using HardenGitHubActions.Cli;
using HardenGitHubActions.Cli.Infrastructure;
using HardenGitHubActions.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli.Testing;

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

    // Test 1 — no args → RepositoryRoot defaults to ".", CommentMode defaults to None
    [Test]
    public async Task Execute_NoArgs_DefaultSettingsParsed()
    {
        var app = BuildTester();

        var result = await app.RunAsync([]);

        var settings = result.Settings as HardenCommand.Settings;
        using (Assert.Multiple())
        {
            await Assert.That(settings).IsNotNull();
            await Assert.That(settings!.RepositoryRoot).IsEqualTo(".");
            await Assert.That(settings.CommentMode).IsEqualTo(TagCommentMode.None);
            await Assert.That(settings.Token).IsNull();
        }
    }

    // Test 2 — --comment-mode ExactTag is parsed into settings
    [Test]
    public async Task Execute_CommentModeExactTag_ParsedIntoSettings()
    {
        var app = BuildTester();

        var result = await app.RunAsync(["--comment-mode", "ExactTag"]);

        var settings = result.Settings as HardenCommand.Settings;
        using (Assert.Multiple())
        {
            await Assert.That(settings).IsNotNull();
            await Assert.That(settings!.CommentMode).IsEqualTo(TagCommentMode.ExactTag);
        }
    }

    // Test 3 — --token is parsed and forwarded to the factory
    [Test]
    public async Task Execute_TokenArg_TokenForwardedToFactory()
    {
        string? capturedToken = null;
        var app = BuildTester(hardenerFactory: (token, _) =>
        {
            capturedToken = token;
            return new WorkflowHardener(_fakeClient);
        });

        await app.RunAsync(["--token", "my-pat", _root]);

        await Assert.That(capturedToken).IsEqualTo("my-pat");
    }

    // Test 4 — successful run (no workflow files) returns exit code 0
    [Test]
    public async Task Execute_SuccessfulRun_ReturnsExitCodeZero()
    {
        var app = BuildTester();

        var result = await app.RunAsync([_root]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
    }

    // Test 5 — GitHubApiException → exit code 1 and error message in output
    [Test]
    public async Task Execute_GitHubApiException_ReturnsExitCodeOneWithMessage()
    {
        var workflowPath = Path.Combine(_root, ".github", "workflows", "ci.yml");
        await File.WriteAllTextAsync(workflowPath, "      - uses: actions/checkout@v4");

        // FakeGitHubApiClient throws GitHubApiException for any unregistered ref
        var app = BuildTester();

        var result = await app.RunAsync([_root]);

        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(result.Output).Contains("GitHub API error");
        }
    }

    // Test 6 — --verbose flag is parsed into settings
    [Test]
    public async Task Execute_VerboseFlag_ParsedIntoSettings()
    {
        var app = BuildTester();

        var result = await app.RunAsync(["--verbose"]);

        var settings = result.Settings as HardenCommand.Settings;
        await Assert.That(settings!.Verbose).IsTrue();
    }

    // Test 7 — --quiet flag is parsed into settings
    [Test]
    public async Task Execute_QuietFlag_ParsedIntoSettings()
    {
        var app = BuildTester();

        var result = await app.RunAsync(["--quiet"]);

        var settings = result.Settings as HardenCommand.Settings;
        await Assert.That(settings!.Quiet).IsTrue();
    }

    // Test 8 — --dry-run flag is parsed and forwarded to hardener options (file not modified)
    [Test]
    public async Task Execute_DryRunFlag_FileNotModified()
    {
        _fakeClient.SetupResolve("actions", "checkout", "v4", "aabbccddaabbccddaabbccddaabbccddaabbccdd");
        var workflowPath = Path.Combine(_root, ".github", "workflows", "ci.yml");
        const string original = "      - uses: actions/checkout@v4";
        await File.WriteAllTextAsync(workflowPath, original);

        var app = BuildTester();
        var result = await app.RunAsync(["--dry-run", _root]);

        var actual = await File.ReadAllTextAsync(workflowPath);
        using (Assert.Multiple())
        {
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(actual).IsEqualTo(original);
        }
    }

    // Test 9 — log level passed to factory: --verbose → LogLevel.Debug
    [Test]
    public async Task Execute_VerboseFlag_PassesDebugLogLevelToFactory()
    {
        LogLevel? capturedLevel = null;
        var app = BuildTester(hardenerFactory: (_, level) =>
        {
            capturedLevel = level;
            return new WorkflowHardener(_fakeClient);
        });

        await app.RunAsync(["--verbose", _root]);

        await Assert.That(capturedLevel).IsEqualTo(LogLevel.Debug);
    }

    // Test 10 — log level passed to factory: --quiet → LogLevel.Warning
    [Test]
    public async Task Execute_QuietFlag_PassesWarningLogLevelToFactory()
    {
        LogLevel? capturedLevel = null;
        var app = BuildTester(hardenerFactory: (_, level) =>
        {
            capturedLevel = level;
            return new WorkflowHardener(_fakeClient);
        });

        await app.RunAsync(["--quiet", _root]);

        await Assert.That(capturedLevel).IsEqualTo(LogLevel.Warning);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private CommandAppTester BuildTester(Func<string?, LogLevel, WorkflowHardener>? hardenerFactory = null)
    {
        hardenerFactory ??= (_, _) => new WorkflowHardener(_fakeClient);

        var services = new ServiceCollection();
        services.AddSingleton(hardenerFactory);

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar: registrar);
        app.SetDefaultCommand<HardenCommand>();
        return app;
    }
}
