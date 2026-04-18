using HardenGitHubActions.Core.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardenGitHubActions.Core;

public sealed record HardeningSummary(
    string ResolvedRepositoryRoot,
    string WorkflowsPath,
    bool WorkflowsDirectoryExists,
    int FilesScanned,
    int FilesModified);

public sealed partial class WorkflowHardener
{
    private readonly IGitHubApiClient _githubClient;
    private readonly ILogger<WorkflowHardener> _logger;

    public WorkflowHardener(IGitHubApiClient githubClient, ILogger<WorkflowHardener>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(githubClient);
        _githubClient = githubClient;
        _logger = logger ?? NullLogger<WorkflowHardener>.Instance;
    }

    public async Task<HardeningSummary> HardenAsync(
        string repositoryRoot,
        HardeningOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(options);

        var discovery = WorkflowScanner.Discover(repositoryRoot);

        LogResolvedRoot(_logger, discovery.ResolvedRepositoryRoot);
        LogWorkflowsPath(_logger, discovery.WorkflowsPath);

        if (!discovery.WorkflowsDirectoryExists)
        {
            LogNoWorkflowsDirectory(_logger, discovery.ResolvedRepositoryRoot);
            return new HardeningSummary(discovery.ResolvedRepositoryRoot, discovery.WorkflowsPath, false, 0, 0);
        }

        if (discovery.Files.Count == 0)
        {
            LogEmptyWorkflowsDirectory(_logger, discovery.WorkflowsPath);
            return new HardeningSummary(discovery.ResolvedRepositoryRoot, discovery.WorkflowsPath, true, 0, 0);
        }

        LogFoundFiles(_logger, discovery.Files.Count);

        var cachingClient = new CachingGitHubApiClient(_githubClient);
        var filesModified = 0;

        foreach (var filePath in discovery.Files)
        {
            ct.ThrowIfCancellationRequested();
            LogProcessingFile(_logger, filePath);

            var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            var rewritten = await WorkflowRewriter.RewriteAsync(content, options, cachingClient, ct).ConfigureAwait(false);

            if (!string.Equals(content, rewritten, StringComparison.Ordinal))
            {
                if (options.DryRun)
                {
                    LogDryRunWouldModify(_logger, filePath);
                }
                else
                {
                    await File.WriteAllTextAsync(filePath, rewritten, ct).ConfigureAwait(false);
                    LogModifiedFile(_logger, filePath);
                }

                filesModified++;
            }
            else
            {
                LogNoChanges(_logger, filePath);
            }
        }

        var summary = new HardeningSummary(
            discovery.ResolvedRepositoryRoot,
            discovery.WorkflowsPath,
            true,
            discovery.Files.Count,
            filesModified);

        LogSummary(_logger, summary.FilesScanned, summary.FilesModified);

        return summary;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved repository root: {Root}")]
    private static partial void LogResolvedRoot(ILogger logger, string root);

    [LoggerMessage(Level = LogLevel.Information, Message = "Workflows directory: {Path}")]
    private static partial void LogWorkflowsPath(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No '.github/workflows' directory found under {Root}. Nothing to do.")]
    private static partial void LogNoWorkflowsDirectory(ILogger logger, string root);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Workflows directory {Path} exists but contains no .yml/.yaml files.")]
    private static partial void LogEmptyWorkflowsDirectory(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} workflow file(s) to process.")]
    private static partial void LogFoundFiles(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing {File}")]
    private static partial void LogProcessingFile(ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dry-run] Would modify {File}")]
    private static partial void LogDryRunWouldModify(ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "Modified {File}")]
    private static partial void LogModifiedFile(ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No changes for {File}")]
    private static partial void LogNoChanges(ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "Summary: scanned {Scanned} file(s), modified {Modified}.")]
    private static partial void LogSummary(ILogger logger, int scanned, int modified);
}
