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

    // Test 1 — no workflow files → HardenAsync completes without API calls
    [Test]
    public async Task HardenAsync_NoWorkflowFiles_CompletesWithoutApiCalls()
    {
        // Remove the workflows directory so there are no files
        Directory.Delete(Path.Combine(_root, ".github", "workflows"), recursive: true);
        var client = new FakeGitHubApiClient();
        var hardener = new WorkflowHardener(client);

        await hardener.HardenAsync(_root, new HardeningOptions());

        using (Assert.Multiple())
        {
            await Assert.That(client.ResolveCallCount).IsEqualTo(0);
            await Assert.That(client.FindMostSpecificCallCount).IsEqualTo(0);
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

        await hardener.HardenAsync(_root, new HardeningOptions { CommentMode = TagCommentMode.None });

        var written = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        await Assert.That(written).IsEqualTo($"      - uses: actions/checkout@{TagSha}");
    }

    // Test 3 — multiple files all processed; same owner/repo@ref resolved only once (cache)
    [Test]
    public async Task HardenAsync_MultipleFiles_SameRefResolvedOnce()
    {
        var client = new FakeGitHubApiClient();
        client.SetupResolve("actions", "checkout", "v4", TagSha);

        // Two files with the same action reference
        CreateWorkflowFile("ci.yml", "      - uses: actions/checkout@v4");
        CreateWorkflowFile("release.yml", "      - uses: actions/checkout@v4");
        var hardener = new WorkflowHardener(client);

        await hardener.HardenAsync(_root, new HardeningOptions { CommentMode = TagCommentMode.None });

        await Assert.That(client.ResolveCallCount).IsEqualTo(1);
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
