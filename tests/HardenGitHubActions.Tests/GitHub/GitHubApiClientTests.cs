using System.Net;
using System.Text;
using HardenGitHubActions.Core.GitHub;

namespace HardenGitHubActions.Tests.GitHub;

/// <summary>
/// A fake HttpMessageHandler that returns pre-canned responses keyed by request URL substring.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string UrlContains, HttpStatusCode Status, string Json)> _responses = [];

    public void AddResponse(string urlContains, HttpStatusCode status, string json) =>
        _responses.Add((urlContains, status, json));

    public List<HttpRequestMessage> SentRequests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        SentRequests.Add(request);

        foreach (var (urlContains, status, json) in _responses)
        {
            if (request.RequestUri?.ToString().Contains(urlContains, StringComparison.Ordinal) == true)
            {
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                });
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });
    }
}

public sealed class GitHubApiClientTests
{
    private static string LightweightTagRefJson(string sha) =>
        $$$"""{"ref":"refs/tags/v4","node_id":"x","url":"u","object":{"sha":"{{{sha}}}","type":"commit","url":"u"}}""";

    private static string AnnotatedTagRefJson(string tagObjectSha) =>
        $$$"""{"ref":"refs/tags/v4","node_id":"x","url":"u","object":{"sha":"{{{tagObjectSha}}}","type":"tag","url":"u"}}""";

    private static string AnnotatedTagObjectJson(string tagObjectSha, string commitSha) =>
        $$$"""{"node_id":"x","tag":"v4","sha":"{{{tagObjectSha}}}","url":"u","message":"m","tagger":{"date":"d","email":"e","name":"n"},"object":{"sha":"{{{commitSha}}}","type":"commit","url":"u"}}""";

    private static string SingleTagMatchingRefsJson(string tagName, string sha) =>
        $$$"""[{"ref":"refs/tags/{{{tagName}}}","node_id":"x","url":"u","object":{"sha":"{{{sha}}}","type":"commit","url":"u"}}]""";

    // Test 1 — lightweight tag (ref object.type == "commit") returns SHA directly
    [Test]
    public async Task ResolveTagToCommitSha_LightweightTag_ReturnsShaDirectly()
    {
        const string expectedSha = "abc1234567890123456789012345678901234567a";
        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("/git/ref/tags/v4", HttpStatusCode.OK, LightweightTagRefJson(expectedSha));

        var client = new GitHubApiClient(new HttpClient(handler));
        var sha = await client.ResolveTagToCommitShaAsync("actions", "checkout", "v4");

        await Assert.That(sha).IsEqualTo(expectedSha);
    }

    // Test 2 — annotated tag (ref object.type == "tag") makes second call and returns inner commit SHA
    [Test]
    public async Task ResolveTagToCommitSha_AnnotatedTag_DereferencesToCommitSha()
    {
        const string tagObjectSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string commitSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("/git/ref/tags/v4", HttpStatusCode.OK, AnnotatedTagRefJson(tagObjectSha));
        handler.AddResponse($"/git/tags/{tagObjectSha}", HttpStatusCode.OK, AnnotatedTagObjectJson(tagObjectSha, commitSha));

        var client = new GitHubApiClient(new HttpClient(handler));
        var sha = await client.ResolveTagToCommitShaAsync("actions", "checkout", "v4");

        await Assert.That(sha).IsEqualTo(commitSha);
    }

    // Test 3 — 404 from ref endpoint throws a typed exception
    [Test]
    public async Task ResolveTagToCommitSha_NotFound_ThrowsGitHubApiException()
    {
        var handler = new FakeHttpMessageHandler();
        // handler returns 404 by default for unmatched URLs

        var client = new GitHubApiClient(new HttpClient(handler));

        await Assert.That(async () =>
            await client.ResolveTagToCommitShaAsync("actions", "checkout", "nonexistent"))
            .Throws<GitHubApiException>();
    }

    // Test 4 — FindMostSpecificTagForSha — single match returns that tag name
    [Test]
    public async Task FindMostSpecificTagForSha_SingleMatch_ReturnsThatTag()
    {
        const string sha = "cccccccccccccccccccccccccccccccccccccccc";
        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("/git/matching-refs/tags/", HttpStatusCode.OK, SingleTagMatchingRefsJson("v4", sha));

        var client = new GitHubApiClient(new HttpClient(handler));
        var tag = await client.FindMostSpecificTagForShaAsync("actions", "checkout", sha);

        await Assert.That(tag).IsEqualTo("v4");
    }

    // Test 5 — FindMostSpecificTagForSha — multiple tags share SHA; v4.2.1 preferred over v4
    [Test]
    public async Task FindMostSpecificTagForSha_MultipleTags_ReturnsMostSpecific()
    {
        const string sha = "dddddddddddddddddddddddddddddddddddddddd";
        const string otherSha = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        var handler = new FakeHttpMessageHandler();
        handler.AddResponse(
            "/git/matching-refs/tags/",
            HttpStatusCode.OK,
            $$$"""
            [
              {"ref":"refs/tags/v4","node_id":"x","url":"u","object":{"sha":"{{{sha}}}","type":"commit","url":"u"}},
              {"ref":"refs/tags/v4.2","node_id":"x","url":"u","object":{"sha":"{{{sha}}}","type":"commit","url":"u"}},
              {"ref":"refs/tags/v4.2.1","node_id":"x","url":"u","object":{"sha":"{{{sha}}}","type":"commit","url":"u"}},
              {"ref":"refs/tags/v3.9.9","node_id":"x","url":"u","object":{"sha":"{{{otherSha}}}","type":"commit","url":"u"}}
            ]
            """);

        var client = new GitHubApiClient(new HttpClient(handler));
        var tag = await client.FindMostSpecificTagForShaAsync("actions", "checkout", sha);

        await Assert.That(tag).IsEqualTo("v4.2.1");
    }

    // Test 6 — FindMostSpecificTagForSha — no match returns null
    [Test]
    public async Task FindMostSpecificTagForSha_NoMatch_ReturnsNull()
    {
        const string sha = "ffffffffffffffffffffffffffffffffffffffff";
        const string otherSha = "0000000000000000000000000000000000000000";
        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("/git/matching-refs/tags/", HttpStatusCode.OK, SingleTagMatchingRefsJson("v1", otherSha));

        var client = new GitHubApiClient(new HttpClient(handler));
        var tag = await client.FindMostSpecificTagForShaAsync("actions", "checkout", sha);

        await Assert.That(tag).IsNull();
    }

    // Test 7 — Bearer token header is sent when token is provided
    [Test]
    public async Task ResolveTagToCommitSha_WithToken_SendsBearerHeader()
    {
        const string expectedSha = "1111111111111111111111111111111111111111";
        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("/git/ref/tags/v1", HttpStatusCode.OK,
            $$$"""{"ref":"refs/tags/v1","node_id":"x","url":"u","object":{"sha":"{{{expectedSha}}}","type":"commit","url":"u"}}""");

        var client = new GitHubApiClient(new HttpClient(handler), token: "my-secret-token");
        await client.ResolveTagToCommitShaAsync("owner", "repo", "v1");

        var sentRequest = handler.SentRequests.Single();
        await Assert.That(sentRequest.Headers.Authorization?.Scheme).IsEqualTo("Bearer");
        await Assert.That(sentRequest.Headers.Authorization?.Parameter).IsEqualTo("my-secret-token");
    }

    // Test 8 — No Authorization header when token is null
    [Test]
    public async Task ResolveTagToCommitSha_NoToken_NoAuthorizationHeader()
    {
        const string expectedSha = "2222222222222222222222222222222222222222";
        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("/git/ref/tags/v1", HttpStatusCode.OK,
            $$$"""{"ref":"refs/tags/v1","node_id":"x","url":"u","object":{"sha":"{{{expectedSha}}}","type":"commit","url":"u"}}""");

        var client = new GitHubApiClient(new HttpClient(handler), token: null);
        await client.ResolveTagToCommitShaAsync("owner", "repo", "v1");

        var sentRequest = handler.SentRequests.Single();
        await Assert.That(sentRequest.Headers.Authorization).IsNull();
    }
}
