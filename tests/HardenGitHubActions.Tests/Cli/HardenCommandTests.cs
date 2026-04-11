using HardenGitHubActions.Cli;
using HardenGitHubActions.Cli.Infrastructure;
using HardenGitHubActions.Core;
using Microsoft.Extensions.DependencyInjection;
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
        var app = BuildTester(hardenerFactory: token =>
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

    // ── helpers ──────────────────────────────────────────────────────────────

    private CommandAppTester BuildTester(Func<string?, WorkflowHardener>? hardenerFactory = null)
    {
        hardenerFactory ??= _ => new WorkflowHardener(_fakeClient);

        var services = new ServiceCollection();
        services.AddSingleton(hardenerFactory);

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar: registrar);
        app.SetDefaultCommand<HardenCommand>();
        return app;
    }
}
