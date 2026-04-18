namespace HardenGitHubActions.Core;

public enum TagCommentMode
{
    None,
    ExactTag,
    MostSpecificTag,
}

public sealed record HardeningOptions
{
    public TagCommentMode CommentMode { get; init; } = TagCommentMode.None;
    public string? GitHubToken { get; init; }
    public bool DryRun { get; init; }
}
