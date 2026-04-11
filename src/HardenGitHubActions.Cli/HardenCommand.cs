using System.Diagnostics.CodeAnalysis;
using HardenGitHubActions.Core;
using HardenGitHubActions.Core.GitHub;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace HardenGitHubActions.Cli;

// Instantiated by Spectre.Console.Cli via reflection
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Spectre.Console.Cli via reflection")]
internal sealed class HardenCommand(IAnsiConsole console, Func<string?, WorkflowHardener> hardenerFactory) : AsyncCommand<HardenCommand.Settings>
{
    // Instantiated by Spectre.Console.Cli via reflection
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Spectre.Console.Cli via reflection")]
    internal sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[repository-root]")]
        [Description("Path to the repository root (default: current directory)")]
        [DefaultValue(".")]
        public string RepositoryRoot { get; init; } = ".";

        [CommandOption("--comment-mode")]
        [Description("Append a tag comment after each pinned SHA (None, ExactTag, MostSpecificTag)")]
        [DefaultValue(TagCommentMode.None)]
        public TagCommentMode CommentMode { get; init; }

        [CommandOption("--token")]
        [Description("GitHub personal access token for authenticated API requests")]
        public string? Token { get; init; }
    }

    private readonly IAnsiConsole _console = console;
    private readonly Func<string?, WorkflowHardener> _hardenerFactory = hardenerFactory;

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(settings.RepositoryRoot);
        var options = new HardeningOptions
        {
            CommentMode = settings.CommentMode,
            GitHubToken = settings.Token,
        };

        var hardener = _hardenerFactory(settings.Token);

        try
        {
            await _console.Status()
                .StartAsync("Hardening workflow files...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await hardener.HardenAsync(root, options, cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);

            _console.MarkupLine("[green]Done.[/]");
            return 0;
        }
        catch (GitHubApiException ex)
        {
            _console.MarkupLine($"[red]GitHub API error ({ex.StatusCode}): {ex.Message}[/]");
            return 1;
        }
    }
}
