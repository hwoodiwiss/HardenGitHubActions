namespace HardenGitHubActions.Core;

public static class WorkflowScanner
{
    private static readonly string[] WorkflowExtensions = [".yml", ".yaml"];

    public static IEnumerable<string> FindWorkflowFiles(string repositoryRoot)
    {
        var workflowsPath = Path.Combine(repositoryRoot, ".github", "workflows");

        if (!Directory.Exists(workflowsPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(workflowsPath, "*", SearchOption.AllDirectories)
            .Where(f => WorkflowExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
    }
}
