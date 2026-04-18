using HardenGitHubActions.Core;
using HardenGitHubActions.Core.GitHub;

namespace HardenGitHubActions.Tests;

/// <summary>
/// A controllable fake IGitHubApiClient for WorkflowRewriter tests.
/// </summary>
internal sealed class FakeGitHubApiClient : IGitHubApiClient
{
    private readonly Dictionary<string, string> _tagToSha = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _shaToMostSpecific = new(StringComparer.Ordinal);

    public int ResolveCallCount { get; private set; }
    public int FindMostSpecificCallCount { get; private set; }

    public void SetupResolve(string owner, string repo, string tag, string sha) =>
        _tagToSha[$"{owner}/{repo}@{tag}"] = sha;

    public void SetupMostSpecific(string sha, string? tag) =>
        _shaToMostSpecific[sha] = tag;

    public Task<string> ResolveTagToCommitShaAsync(string owner, string repo, string tag, CancellationToken ct = default)
    {
        ResolveCallCount++;
        var key = $"{owner}/{repo}@{tag}";
        return _tagToSha.TryGetValue(key, out var sha)
            ? Task.FromResult(sha)
            : throw new GitHubApiException(404, $"No fake SHA set up for {key}");
    }

    public Task<string?> FindMostSpecificTagForShaAsync(string owner, string repo, string sha, CancellationToken ct = default)
    {
        FindMostSpecificCallCount++;
        return _shaToMostSpecific.TryGetValue(sha, out var tag)
            ? Task.FromResult(tag)
            : Task.FromResult<string?>(null);
    }
}

public sealed class WorkflowRewriterTests
{
    private const string ResolveSha = "aabbccddaabbccddaabbccddaabbccddaabbccdd";
    private const string AlreadyPinnedSha = "1122334411223344112233441122334411223344";

    private static FakeGitHubApiClient CreateClient(
        string owner = "actions",
        string repo = "checkout",
        string tag = "v4",
        string? mostSpecificTag = null)
    {
        var client = new FakeGitHubApiClient();
        client.SetupResolve(owner, repo, tag, ResolveSha);
        client.SetupMostSpecific(ResolveSha, mostSpecificTag ?? tag);
        client.SetupMostSpecific(AlreadyPinnedSha, mostSpecificTag ?? tag);
        return client;
    }

    // Test 1 — line with no uses: is returned unchanged
    [Test]
    public async Task RewriteAsync_NoUsesLine_ReturnedUnchanged()
    {
        const string content = "name: CI\non: push";

        var result = await WorkflowRewriter.RewriteAsync(content, new HardeningOptions(), CreateClient());

        await Assert.That(result).IsEqualTo(content);
    }

    // Test 2 — uses: owner/repo@v4 rewritten to SHA (CommentMode=None)
    [Test]
    public async Task RewriteAsync_TagRef_RewrittenToShaNoComment()
    {
        const string content = "      - uses: actions/checkout@v4";
        var options = new HardeningOptions { CommentMode = TagCommentMode.None };

        var result = await WorkflowRewriter.RewriteAsync(content, options, CreateClient());

        await Assert.That(result).IsEqualTo($"      - uses: actions/checkout@{ResolveSha}");
    }

    // Test 3 — CommentMode=ExactTag appends # v4
    [Test]
    public async Task RewriteAsync_ExactTagMode_AppendsOriginalTagComment()
    {
        const string content = "      - uses: actions/checkout@v4";
        var options = new HardeningOptions { CommentMode = TagCommentMode.ExactTag };

        var result = await WorkflowRewriter.RewriteAsync(content, options, CreateClient());

        await Assert.That(result).IsEqualTo($"      - uses: actions/checkout@{ResolveSha}  # v4");
    }

    // Test 4 — CommentMode=MostSpecificTag calls FindMostSpecificTagForShaAsync and appends result
    [Test]
    public async Task RewriteAsync_MostSpecificTagMode_AppendsMostSpecificTagComment()
    {
        const string content = "      - uses: actions/checkout@v4";
        var client = CreateClient(mostSpecificTag: "v4.2.1");
        var options = new HardeningOptions { CommentMode = TagCommentMode.MostSpecificTag };

        var result = await WorkflowRewriter.RewriteAsync(content, options, client);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsEqualTo($"      - uses: actions/checkout@{ResolveSha}  # v4.2.1");
            await Assert.That(client.FindMostSpecificCallCount).IsGreaterThan(0);
        }
    }

    // Test 5 — already-SHA-pinned, CommentMode=None → line unchanged
    [Test]
    public async Task RewriteAsync_AlreadyPinned_CommentModeNone_LineUnchanged()
    {
        var content = $"      - uses: actions/checkout@{AlreadyPinnedSha}";
        var client = CreateClient();
        var options = new HardeningOptions { CommentMode = TagCommentMode.None };

        var result = await WorkflowRewriter.RewriteAsync(content, options, client);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsEqualTo(content);
            await Assert.That(client.ResolveCallCount).IsEqualTo(0);
        }
    }

    // Test 6 — already-SHA-pinned, CommentMode=ExactTag → comment added, no resolve call
    [Test]
    public async Task RewriteAsync_AlreadyPinned_CommentModeExactTag_CommentAddedNoResolve()
    {
        var content = $"      - uses: actions/checkout@{AlreadyPinnedSha}";
        var client = CreateClient();
        var options = new HardeningOptions { CommentMode = TagCommentMode.ExactTag };

        var result = await WorkflowRewriter.RewriteAsync(content, options, client);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsEqualTo($"      - uses: actions/checkout@{AlreadyPinnedSha}  # v4");
            await Assert.That(client.ResolveCallCount).IsEqualTo(0);
        }
    }

    // Test 7 — uses: ./local → unchanged
    [Test]
    public async Task RewriteAsync_LocalPath_Unchanged()
    {
        const string content = "      - uses: ./local/action";

        var result = await WorkflowRewriter.RewriteAsync(content, new HardeningOptions(), CreateClient());

        await Assert.That(result).IsEqualTo(content);
    }

    // Test 8 — uses: docker:// → unchanged
    [Test]
    public async Task RewriteAsync_Docker_Unchanged()
    {
        const string content = "      - uses: docker://alpine:3.18";

        var result = await WorkflowRewriter.RewriteAsync(content, new HardeningOptions(), CreateClient());

        await Assert.That(result).IsEqualTo(content);
    }

    // Test 9 — existing trailing comment replaced, not doubled
    [Test]
    public async Task RewriteAsync_ExistingComment_ReplacedNotDoubled()
    {
        var content = $"      - uses: actions/checkout@{AlreadyPinnedSha}  # old-comment";
        var options = new HardeningOptions { CommentMode = TagCommentMode.ExactTag };

        var result = await WorkflowRewriter.RewriteAsync(content, options, CreateClient());

        await Assert.That(result).IsEqualTo($"      - uses: actions/checkout@{AlreadyPinnedSha}  # v4");
    }

    // Test 10 — leading whitespace and - prefix preserved
    [Test]
    [Arguments("        - uses: actions/checkout@v4", 8, true)]
    [Arguments("    uses: actions/checkout@v4", 4, false)]
    [Arguments("uses: actions/checkout@v4", 0, false)]
    public async Task RewriteAsync_IndentationPreserved(string content, int spaces, bool hasDash)
    {
        var options = new HardeningOptions { CommentMode = TagCommentMode.None };

        var result = await WorkflowRewriter.RewriteAsync(content, options, CreateClient());

        var expectedPrefix = new string(' ', spaces) + (hasDash ? "- " : string.Empty);
        await Assert.That(result).StartsWith(expectedPrefix + "uses:");
    }

    // Test 11 — multiple uses: in same file all rewritten
    [Test]
    public async Task RewriteAsync_MultipleUsesLines_AllRewritten()
    {
        var client = new FakeGitHubApiClient();
        const string sha1 = "aaaa000000000000000000000000000000000000";
        const string sha2 = "bbbb000000000000000000000000000000000000";
        client.SetupResolve("actions", "checkout", "v4", sha1);
        client.SetupResolve("actions", "setup-node", "v3", sha2);

        const string content = """
            steps:
              - uses: actions/checkout@v4
              - uses: actions/setup-node@v3
            """;

        var options = new HardeningOptions { CommentMode = TagCommentMode.None };

        var result = await WorkflowRewriter.RewriteAsync(content, options, client);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains($"actions/checkout@{sha1}");
            await Assert.That(result).Contains($"actions/setup-node@{sha2}");
        }
    }

    // Test 12 — CRLF line endings: lines must still be matched and CRLF preserved
    [Test]
    public async Task RewriteAsync_CrlfLineEndings_RewrittenAndPreserved()
    {
        var client = new FakeGitHubApiClient();
        const string sha1 = "aaaa000000000000000000000000000000000000";
        client.SetupResolve("actions", "checkout", "v4", sha1);

        const string content = "steps:\r\n  - uses: actions/checkout@v4\r\n";
        var options = new HardeningOptions { CommentMode = TagCommentMode.None };

        var result = await WorkflowRewriter.RewriteAsync(content, options, client);

        await Assert.That(result).IsEqualTo($"steps:\r\n  - uses: actions/checkout@{sha1}\r\n");
    }
}
