using HardenGitHubActions.Core;

namespace HardenGitHubActions.Tests;

public sealed class WorkflowScannerDiscoverTests : IDisposable
{
    private readonly string _root;

    public WorkflowScannerDiscoverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"hga-scanner-discover-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Test 1 — Discover resolves the root path and reports no directory when absent
    [Test]
    public async Task Discover_NoWorkflowsDirectory_ReportsNotExists()
    {
        var result = WorkflowScanner.Discover(_root);

        using (Assert.Multiple())
        {
            await Assert.That(result.WorkflowsDirectoryExists).IsFalse();
            await Assert.That(result.Files).IsEmpty();
            await Assert.That(result.ResolvedRepositoryRoot).IsEqualTo(Path.GetFullPath(_root));
        }
    }

    // Test 2 — Discover reports directory exists but empty when no yml/yaml present
    [Test]
    public async Task Discover_WorkflowsDirectoryExistsButEmpty_ReportsExistsWithNoFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".github", "workflows"));

        var result = WorkflowScanner.Discover(_root);

        using (Assert.Multiple())
        {
            await Assert.That(result.WorkflowsDirectoryExists).IsTrue();
            await Assert.That(result.Files).IsEmpty();
        }
    }

    // Test 3 — Discover returns correct WorkflowsPath
    [Test]
    public async Task Discover_AnyRoot_WorkflowsPathIsCorrect()
    {
        var result = WorkflowScanner.Discover(_root);

        var expectedPath = Path.Combine(Path.GetFullPath(_root), ".github", "workflows");
        await Assert.That(result.WorkflowsPath).IsEqualTo(expectedPath);
    }

    // Test 4 — Discover returns .yml and .yaml files
    [Test]
    public async Task Discover_YmlAndYamlFiles_BothReturned()
    {
        var workflowsDir = Path.Combine(_root, ".github", "workflows");
        Directory.CreateDirectory(workflowsDir);
        var ymlPath = Path.Combine(workflowsDir, "ci.yml");
        var yamlPath = Path.Combine(workflowsDir, "release.yaml");
        await File.WriteAllTextAsync(ymlPath, string.Empty);
        await File.WriteAllTextAsync(yamlPath, string.Empty);

        var result = WorkflowScanner.Discover(_root);

        using (Assert.Multiple())
        {
            await Assert.That(result.Files).Contains(ymlPath);
            await Assert.That(result.Files).Contains(yamlPath);
            await Assert.That(result.Files.Count).IsEqualTo(2);
        }
    }

    // Test 5 — FindWorkflowFiles shim still works (backwards compat)
    [Test]
    public async Task FindWorkflowFiles_ShimStillWorks()
    {
        var workflowsDir = Path.Combine(_root, ".github", "workflows");
        Directory.CreateDirectory(workflowsDir);
        await File.WriteAllTextAsync(Path.Combine(workflowsDir, "ci.yml"), string.Empty);

        var files = WorkflowScanner.FindWorkflowFiles(_root).ToList();

        await Assert.That(files.Count).IsEqualTo(1);
    }
}
