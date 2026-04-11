using HardenGitHubActions.Core;

namespace HardenGitHubActions.Tests;

public sealed class WorkflowScannerTests : IDisposable
{
    private readonly string _root;

    public WorkflowScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"hga-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Test 1 — empty repo root returns no files
    [Test]
    public async Task FindWorkflowFiles_EmptyRoot_ReturnsNoFiles()
    {
        var files = WorkflowScanner.FindWorkflowFiles(_root);

        await Assert.That(files).IsEmpty();
    }

    // Test 2 — missing .github/workflows directory returns no files
    [Test]
    public async Task FindWorkflowFiles_NoWorkflowsDirectory_ReturnsNoFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".github"));

        var files = WorkflowScanner.FindWorkflowFiles(_root);

        await Assert.That(files).IsEmpty();
    }

    // Test 3 — .yml and .yaml files are returned
    [Test]
    public async Task FindWorkflowFiles_YmlAndYamlFiles_BothReturned()
    {
        var workflowsDir = CreateWorkflowsDir();
        var ymlPath = CreateFile(workflowsDir, "ci.yml");
        var yamlPath = CreateFile(workflowsDir, "release.yaml");

        var files = WorkflowScanner.FindWorkflowFiles(_root).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(files).Contains(ymlPath);
            await Assert.That(files).Contains(yamlPath);
            await Assert.That(files.Count).IsEqualTo(2);
        }
    }

    // Test 4 — files outside .github/workflows are not returned
    [Test]
    public async Task FindWorkflowFiles_FilesOutsideWorkflowsDir_NotReturned()
    {
        CreateWorkflowsDir();
        var outsidePath = CreateFile(_root, "some-workflow.yml");

        var files = WorkflowScanner.FindWorkflowFiles(_root).ToList();

        await Assert.That(files).DoesNotContain(outsidePath);
    }

    // Test 5 — files in subdirectories of .github/workflows are returned
    [Test]
    public async Task FindWorkflowFiles_NestedSubdirectory_FilesReturned()
    {
        var workflowsDir = CreateWorkflowsDir();
        var subDir = Path.Combine(workflowsDir, "sub");
        Directory.CreateDirectory(subDir);
        var nestedPath = CreateFile(subDir, "nested.yml");

        var files = WorkflowScanner.FindWorkflowFiles(_root).ToList();

        await Assert.That(files).Contains(nestedPath);
    }

    private string CreateWorkflowsDir()
    {
        var path = Path.Combine(_root, ".github", "workflows");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFile(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}
