namespace HardenGitHubActions.Core.GitHub;

public interface IGitHubApiClient
{
    Task<string> ResolveTagToCommitShaAsync(string owner, string repo, string tag, CancellationToken ct = default);
    Task<string?> FindMostSpecificTagForShaAsync(string owner, string repo, string sha, CancellationToken ct = default);
}
