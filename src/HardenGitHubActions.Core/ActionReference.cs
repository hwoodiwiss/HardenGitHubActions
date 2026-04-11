using System.Text.RegularExpressions;

namespace HardenGitHubActions.Core;

public sealed record ActionReference
{
    private static readonly Regex OwnerRepoRefPattern = new(
        @"^(?<owner>[^/\s]+)/(?<repo>[^@\s]+)@(?<ref>\S+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ShaPattern = new(
        @"^[0-9a-fA-F]{40}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Owner { get; init; } = string.Empty;
    public string Repo { get; init; } = string.Empty;
    public string Ref { get; init; } = string.Empty;
    public bool IsAlreadyPinned { get; init; }
    public bool IsLocalPath { get; init; }
    public bool IsDocker { get; init; }

    public static bool TryParse(string uses, out ActionReference? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(uses))
        {
            return false;
        }

        if (uses.StartsWith("./", StringComparison.Ordinal))
        {
            result = new ActionReference { IsLocalPath = true };
            return true;
        }

        if (uses.StartsWith("docker://", StringComparison.Ordinal))
        {
            result = new ActionReference { IsDocker = true };
            return true;
        }

        var match = OwnerRepoRefPattern.Match(uses);
        if (!match.Success)
        {
            return false;
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value;
        var @ref = match.Groups["ref"].Value;

        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
        {
            return false;
        }

        result = new ActionReference
        {
            Owner = owner,
            Repo = repo,
            Ref = @ref,
            IsAlreadyPinned = ShaPattern.IsMatch(@ref),
        };

        return true;
    }
}
