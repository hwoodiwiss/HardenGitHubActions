using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using HardenGitHubActions.Core;
using HardenGitHubActions.Core.GitHub;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HardenGitHubActions.Cli;

// Instantiated by Spectre.Console.Cli via reflection
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Spectre.Console.Cli via reflection")]
internal sealed class HardenCommand(IAnsiConsole console, Func<string?, LogLevel, WorkflowHardener> hardenerFactory) : AsyncCommand<HardenCommand.Settings>
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

        [CommandOption("-v|--verbose")]
        [Description("Enable verbose (Debug) logging")]
        public bool Verbose { get; init; }

        [CommandOption("-q|--quiet")]
        [Description("Suppress informational output (Warnings and above only)")]
        public bool Quiet { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show what would change without writing any files")]
        public bool DryRun { get; init; }
    }

    private readonly IAnsiConsole _console = console;
    private readonly Func<string?, LogLevel, WorkflowHardener> _hardenerFactory = hardenerFactory;

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var logLevel = settings switch
        {
            { Verbose: true } => LogLevel.Debug,
            { Quiet: true }   => LogLevel.Warning,
            _                 => LogLevel.Information,
        };

        var options = new HardeningOptions
        {
            CommentMode = settings.CommentMode,
            GitHubToken = settings.Token,
            DryRun = settings.DryRun,
        };

        var hardener = _hardenerFactory(settings.Token, logLevel);

        try
        {
            var summary = await hardener.HardenAsync(settings.RepositoryRoot, options, cancellationToken).ConfigureAwait(false);

            if (settings.DryRun)
            {
                _console.MarkupLine("[yellow]Dry-run complete.[/]");
            }
            else
            {
                _console.MarkupLine("[green]Done.[/]");
            }

            _console.MarkupLine($"[dim]Scanned {summary.FilesScanned} file(s), modified {summary.FilesModified}.[/]");

            return 0;
        }
        catch (GitHubApiException ex)
        {
            _console.MarkupLine($"[red]GitHub API error ({ex.StatusCode}): {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
