using HardenGitHubActions.Core;

namespace HardenGitHubActions.Tests;

public sealed class WorkflowHardenerTests : IDisposable
{
    private readonly string _root;

    private const string TagSha = "aabbccddaabbccddaabbccddaabbccddaabbccdd";

    public WorkflowHardenerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"hga-hardener-{Guid.NewGuid():N}");
        CreateWorkflowsDir();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Test 1 — no workflows directory → HardenAsync returns summary with WorkflowsDirectoryExists=false
    [Test]
    public async Task HardenAsync_NoWorkflowsDirectory_ReturnsSummaryWithDirectoryNotFound()
    {
        Directory.Delete(Path.Combine(_root, ".github", "workflows"), recursive: true);
        var client = new FakeGitHubApiClient();
        var hardener = new WorkflowHardener(client);

        var summary = await hardener.HardenAsync(_root, new HardeningOptions());

        using (Assert.Multiple())
        {
            await Assert.That(client.ResolveCallCount).IsEqualTo(0);
            await Assert.That(summary.WorkflowsDirectoryExists).IsFalse();
            await Assert.That(summary.FilesScanned).IsEqualTo(0);
            await Assert.That(summary.FilesModified).IsEqualTo(0);
        }
    }

    // Test 2 — single workflow file is read, rewritten, and written back
    [Test]
    public async Task HardenAsync_SingleFile_RewrittenAndSavedToDisk()
    {
        var client = new FakeGitHubApiClient();
        client.SetupResolve("actions", "checkout", "v4", TagSha);

        var filePath = CreateWorkflowFile("ci.yml", "      - uses: actions/checkout@v4");
        var hardener = new WorkflowHardener(client);

        var summary = await hardener.HardenAsync(_root, new HardeningOptions { CommentMode = TagCommentMode.None });

        var written = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        using (Assert.Multiple())
        {
            await Assert.That(written).IsEqualTo($"      - uses: actions/checkout@{TagSha}");
            await Assert.That(summary.FilesScanned).IsEqualTo(1);
            await Assert.That(summary.FilesModified).IsEqualTo(1);
        }
    }

    // Test 3 — multiple files all processed; same owner/repo@ref resolved only once (cache)
    [Test]
    public async Task HardenAsync_MultipleFiles_SameRefResolvedOnce()
    {
        var client = new FakeGitHubApiClient();
        client.SetupResolve("actions", "checkout", "v4", TagSha);

        CreateWorkflowFile("ci.yml", "      - uses: actions/checkout@v4");
        CreateWorkflowFile("release.yml", "      - uses: actions/checkout@v4");
        var hardener = new WorkflowHardener(client);

        var summary = await hardener.HardenAsync(_root, new HardeningOptions { CommentMode = TagCommentMode.None });

        using (Assert.Multiple())
        {
            await Assert.That(client.ResolveCallCount).IsEqualTo(1);
            await Assert.That(summary.FilesScanned).IsEqualTo(2);
            await Assert.That(summary.FilesModified).IsEqualTo(2);
        }
    }

    // Test 4 — CommentMode option is threaded through to the rewriter
    [Test]
    public async Task HardenAsync_ExactTagCommentMode_CommentAppendedInOutput()
    {
        var client = new FakeGitHubApiClient();
        client.SetupResolve("actions", "checkout", "v4", TagSha);
        client.SetupMostSpecific(TagSha, "v4");

        var filePath = CreateWorkflowFile("ci.yml", "      - uses: actions/checkout@v4");
        var hardener = new WorkflowHardener(client);

        await hardener.HardenAsync(_root, new HardeningOptions { CommentMode = TagCommentMode.ExactTag });

        var written = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        await Assert.That(written).IsEqualTo($"      - uses: actions/checkout@{TagSha}  # v4");
    }

    // Test 5 — DryRun does not write files
    [Test]
    public async Task HardenAsync_DryRun_DoesNotModifyFiles()
    {
        var client = new FakeGitHubApiClient();
        client.SetupResolve("actions", "checkout", "v4", TagSha);

        const string original = "      - uses: actions/checkout@v4";
        var filePath = CreateWorkflowFile("ci.yml", original);
        var hardener = new WorkflowHardener(client);

        var summary = await hardener.HardenAsync(_root, new HardeningOptions { DryRun = true });

        var actual = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        using (Assert.Multiple())
        {
            await Assert.That(actual).IsEqualTo(original);
            await Assert.That(summary.FilesModified).IsEqualTo(1); // would-be modification counted
        }
    }

    // Test 6 — already-pinned file with no changes → FilesModified = 0
    [Test]
    public async Task HardenAsync_AlreadyPinnedFile_FilesModifiedIsZero()
    {
        var client = new FakeGitHubApiClient();

        var filePath = CreateWorkflowFile("ci.yml", $"      - uses: actions/checkout@{TagSha}");
        var hardener = new WorkflowHardener(client);

        var summary = await hardener.HardenAsync(_root, new HardeningOptions());

        using (Assert.Multiple())
        {
            await Assert.That(summary.FilesScanned).IsEqualTo(1);
            await Assert.That(summary.FilesModified).IsEqualTo(0);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private string CreateWorkflowsDir()
    {
        var path = Path.Combine(_root, ".github", "workflows");
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateWorkflowFile(string name, string content)
    {
        var path = Path.Combine(_root, ".github", "workflows", name);
        File.WriteAllText(path, content);
        return path;
    }
}
