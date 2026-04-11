namespace HardenGitHubActions.Core.GitHub;

/// <summary>
/// Wraps an <see cref="IGitHubApiClient"/> and memoises results within a single
/// <see cref="WorkflowHardener.HardenAsync"/> call so the same tag is never
/// resolved twice across multiple workflow files.
/// </summary>
internal sealed class CachingGitHubApiClient : IGitHubApiClient
{
    private readonly IGitHubApiClient _inner;
    private readonly Dictionary<string, string> _resolveCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _mostSpecificCache = new(StringComparer.Ordinal);

    internal CachingGitHubApiClient(IGitHubApiClient inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public async Task<string> ResolveTagToCommitShaAsync(
        string owner, string repo, string tag, CancellationToken ct = default)
    {
        var key = $"{owner}/{repo}@{tag}";
        if (_resolveCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var sha = await _inner.ResolveTagToCommitShaAsync(owner, repo, tag, ct).ConfigureAwait(false);
        _resolveCache[key] = sha;
        return sha;
    }

    public async Task<string?> FindMostSpecificTagForShaAsync(
        string owner, string repo, string sha, CancellationToken ct = default)
    {
        var key = $"{owner}/{repo}#{sha}";
        if (_mostSpecificCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var tag = await _inner.FindMostSpecificTagForShaAsync(owner, repo, sha, ct).ConfigureAwait(false);
        _mostSpecificCache[key] = tag;
        return tag;
    }
}
