using HardenGitHubActions.Core.GitHub;

namespace HardenGitHubActions.Core;

public sealed class WorkflowHardener
{
    private readonly IGitHubApiClient _githubClient;

    public WorkflowHardener(IGitHubApiClient githubClient)
    {
        ArgumentNullException.ThrowIfNull(githubClient);
        _githubClient = githubClient;
    }

    public async Task HardenAsync(
        string repositoryRoot,
        HardeningOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(options);

        var files = WorkflowScanner.FindWorkflowFiles(repositoryRoot);
        var cachingClient = new CachingGitHubApiClient(_githubClient);

        foreach (var filePath in files)
        {
            var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            var rewritten = await WorkflowRewriter.RewriteAsync(content, options, cachingClient, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(filePath, rewritten, ct).ConfigureAwait(false);
        }
    }
}
