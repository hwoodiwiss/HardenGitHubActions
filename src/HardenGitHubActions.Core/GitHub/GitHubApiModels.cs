using System.Text.Json.Serialization;

namespace HardenGitHubActions.Core.GitHub;

[JsonSerializable(typeof(GitRefResponse))]
[JsonSerializable(typeof(GitTagResponse))]
[JsonSerializable(typeof(GitMatchingRef[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;


internal sealed record GitRefResponse(GitRefObject Object);

internal sealed record GitRefObject(string Sha, string Type);

internal sealed record GitTagResponse(GitTagObject Object);

internal sealed record GitTagObject(string Sha, string Type);

internal sealed record GitMatchingRef(string Ref, GitMatchingRefObject Object);

internal sealed record GitMatchingRefObject(string Sha);
