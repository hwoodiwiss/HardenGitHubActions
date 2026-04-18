namespace HardenGitHubActions.Core;

public sealed record WorkflowDiscoveryResult(
    string ResolvedRepositoryRoot,
    string WorkflowsPath,
    bool WorkflowsDirectoryExists,
    IReadOnlyList<string> Files);

public static class WorkflowScanner
{
    private static readonly string[] WorkflowExtensions = [".yml", ".yaml"];

    public static WorkflowDiscoveryResult Discover(string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);

        var resolvedRoot = Path.GetFullPath(repositoryRoot);
        var workflowsPath = Path.Combine(resolvedRoot, ".github", "workflows");

        if (!Directory.Exists(workflowsPath))
        {
            return new WorkflowDiscoveryResult(resolvedRoot, workflowsPath, false, []);
        }

        var files = Directory.EnumerateFiles(workflowsPath, "*", SearchOption.AllDirectories)
            .Where(f => WorkflowExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return new WorkflowDiscoveryResult(resolvedRoot, workflowsPath, true, files);
    }

    // Kept for backward compatibility with any external consumers.
    public static IEnumerable<string> FindWorkflowFiles(string repositoryRoot)
        => Discover(repositoryRoot).Files;
}
