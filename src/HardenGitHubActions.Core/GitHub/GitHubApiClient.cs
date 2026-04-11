using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace HardenGitHubActions.Core.GitHub;

public sealed class GitHubApiClient : IGitHubApiClient
{
    private const string ApiBase = "https://api.github.com";

    private readonly HttpClient _httpClient;

    public GitHubApiClient(HttpClient httpClient, string? token = null)
    {
        _httpClient = httpClient;

        _httpClient.BaseAddress = new Uri(ApiBase);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("HardenGitHubActions", "1.0"));

        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<string> ResolveTagToCommitShaAsync(
        string owner,
        string repo,
        string tag,
        CancellationToken ct = default)
    {
        var refResponse = await GetAsync(
            $"/repos/{owner}/{repo}/git/ref/tags/{tag}",
            GitHubJsonContext.Default.GitRefResponse,
            ct).ConfigureAwait(false);

        if (refResponse.Object.Type == "commit")
        {
            return refResponse.Object.Sha;
        }

        // Annotated tag — dereference the tag object to get the commit SHA
        var tagResponse = await GetAsync(
            $"/repos/{owner}/{repo}/git/tags/{refResponse.Object.Sha}",
            GitHubJsonContext.Default.GitTagResponse,
            ct).ConfigureAwait(false);

        return tagResponse.Object.Sha;
    }

    public async Task<string?> FindMostSpecificTagForShaAsync(
        string owner,
        string repo,
        string sha,
        CancellationToken ct = default)
    {
        var allRefs = await GetAsync(
            $"/repos/{owner}/{repo}/git/matching-refs/tags/",
            GitHubJsonContext.Default.GitMatchingRefArray,
            ct).ConfigureAwait(false);

        var matchingTagNames = allRefs
            .Where(r => string.Equals(r.Object.Sha, sha, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Ref.Replace("refs/tags/", string.Empty, StringComparison.Ordinal))
            .ToList();

        if (matchingTagNames.Count == 0)
        {
            return null;
        }

        return matchingTagNames
            .OrderByDescending(t => t.Split('.').Length)
            .ThenByDescending(t => t, StringComparer.Ordinal)
            .First();
    }

    private async Task<T> GetAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(new Uri(path, UriKind.Relative), ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubApiException(
                (int)response.StatusCode,
                $"GitHub API request to {path} failed with status {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(content, typeInfo)
            ?? throw new GitHubApiException(0, $"GitHub API returned null response for {path}");
    }
}

