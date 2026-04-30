using System.CommandLine;
using HardenGitHubActions.Cli.Inputs;
using HardenGitHubActions.Core;
using HardenGitHubActions.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace HardenGitHubActions.Cli;

internal sealed class HardenCommand(IAnsiConsole console, Func<string?, LogLevel, WorkflowHardener> hardenerFactory)
{
    internal static class Inputs
    {
        public static RepositoryRootArgument RepositoryRoot { get; } = new RepositoryRootArgument();

        public static CommentModeOption CommentMode { get; } = new CommentModeOption();

        public static GitHubTokenOption GitHubToken { get; } = new GitHubTokenOption();

        public static VerboseFlag Verbose { get; } = new VerboseFlag();

        public static QuietFlag Quiet { get; } = new QuietFlag();

        public static DryRunFlag DryRun { get; } = new DryRunFlag();
    }

    internal sealed record Settings(string RepositoryRoot, TagCommentMode CommentMode, string? Token, bool Verbose, bool Quiet, bool DryRun);

    private readonly IAnsiConsole _console = console;
    private readonly Func<string?, LogLevel, WorkflowHardener> _hardenerFactory = hardenerFactory;

    internal static RootCommand BuildRootCommand(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var rootCommand = new RootCommand("Harden GitHub Actions workflow files by pinning action versions to specific SHAs")
        {
            Inputs.RepositoryRoot,
            Inputs.CommentMode,
            Inputs.GitHubToken,
            Inputs.Verbose,
            Inputs.Quiet,
            Inputs.DryRun,
        };

        rootCommand.SetAction((parseResult, cancellationToken)
            => RootCommandActionAsync(parseResult, serviceProvider, cancellationToken));

        return rootCommand;
    }

    internal static async Task<int> RootCommandActionAsync(ParseResult parseResult, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var repositoryRoot = parseResult.GetRequiredValue<string>(Inputs.RepositoryRoot);
        var commentMode = parseResult.GetRequiredValue(Inputs.CommentMode);
        var token = parseResult.GetRequiredValue(Inputs.GitHubToken);
        var verbose = parseResult.GetRequiredValue(Inputs.Verbose);
        var quiet = parseResult.GetRequiredValue(Inputs.Quiet);
        var dryRun = parseResult.GetRequiredValue(Inputs.DryRun);

        var settings = new Settings(repositoryRoot, commentMode, token, verbose, quiet, dryRun);
        var command = serviceProvider.GetRequiredService<HardenCommand>();
        return await command.ExecuteAsync(settings, cancellationToken);
    }

    internal async Task<int> ExecuteAsync(Settings settings, CancellationToken cancellationToken)
    {
        var logLevel = settings switch
        {
            { Verbose: true } => LogLevel.Debug,
            { Quiet: true } => LogLevel.Warning,
            _ => LogLevel.Information,
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
