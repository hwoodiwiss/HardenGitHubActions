using System.CommandLine;
using System.CommandLine.Parsing;

namespace HardenGitHubActions.Cli.Options;

public sealed class GitHubTokenOption : Option<string?>
{
    public GitHubTokenOption() : base("--token")
    {
        Description = "GitHub personal access token for authenticated API requests";
        Arity = ArgumentArity.ZeroOrOne;
        DefaultValueFactory = GetDefaultValue;
    }

    private static string? GetDefaultValue(ArgumentResult _) => null;
}