using System.Text;
using System.Text.RegularExpressions;
using HardenGitHubActions.Core.GitHub;

namespace HardenGitHubActions.Core;

public sealed partial class WorkflowRewriter
{
    // Matches: {leading-whitespace}{optional-dash-space}uses: {ref}{optional-comment}
    [GeneratedRegex(@"^(?<prefix>\s*(?:-\s+)?)uses:\s+(?<uses>\S+?)(?:\s*#.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex UsesLinePattern { get; }

    public static async Task<string> RewriteAsync(
        string fileContent,
        HardeningOptions options,
        IGitHubApiClient github,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileContent);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(github);

        var lines = fileContent.Split('\n');
        var result = new StringBuilder(fileContent.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var rewritten = await RewriteLineAsync(line, options, github, ct).ConfigureAwait(false);
            result.Append(rewritten);

            if (i < lines.Length - 1)
            {
                result.Append('\n');
            }
        }

        return result.ToString();
    }

    private static async Task<string> RewriteLineAsync(
        string line,
        HardeningOptions options,
        IGitHubApiClient github,
        CancellationToken ct)
    {
        var match = UsesLinePattern.Match(line);
        if (!match.Success)
        {
            return line;
        }

        if (!ActionReference.TryParse(match.Groups["uses"].Value, out var actionRef) || actionRef is null)
        {
            return line;
        }

        if (actionRef.IsLocalPath || actionRef.IsDocker)
        {
            return line;
        }

        var prefix = match.Groups["prefix"].Value;

        if (actionRef.IsAlreadyPinned)
        {
            if (options.CommentMode == TagCommentMode.None)
            {
                return line;
            }

            var comment = await GetCommentAsync(
                actionRef.Owner, actionRef.Repo, actionRef.Ref, options, github, ct).ConfigureAwait(false);

            return BuildLine(prefix, actionRef.Owner, actionRef.Repo, actionRef.Ref, comment);
        }

        var sha = await github.ResolveTagToCommitShaAsync(
            actionRef.Owner, actionRef.Repo, actionRef.Ref, ct).ConfigureAwait(false);

        var tagComment = options.CommentMode switch
        {
            TagCommentMode.ExactTag => actionRef.Ref,
            TagCommentMode.MostSpecificTag => await github.FindMostSpecificTagForShaAsync(actionRef.Owner, actionRef.Repo, sha, ct).ConfigureAwait(false),
            TagCommentMode.None or _ => null,
        };

        return BuildLine(prefix, actionRef.Owner, actionRef.Repo, sha, tagComment);
    }

    private static async Task<string?> GetCommentAsync(
        string owner,
        string repo,
        string sha,
        HardeningOptions options,
        IGitHubApiClient github,
        CancellationToken ct)
    {
        return options.CommentMode switch
        {
            TagCommentMode.ExactTag or TagCommentMode.MostSpecificTag =>
                await github.FindMostSpecificTagForShaAsync(owner, repo, sha, ct).ConfigureAwait(false),
            TagCommentMode.None or _ => null,
        };
    }

    private static string BuildLine(string prefix, string owner, string repo, string sha, string? comment)
    {
        var uses = $"{prefix}uses: {owner}/{repo}@{sha}";
        return comment is not null ? $"{uses}  # {comment}" : uses;
    }
}
