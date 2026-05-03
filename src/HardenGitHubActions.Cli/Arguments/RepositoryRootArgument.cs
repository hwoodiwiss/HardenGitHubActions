using System.CommandLine;
using System.CommandLine.Parsing;

namespace HardenGitHubActions.Cli.Arguments;

public sealed class RepositoryRootArgument : Argument<string>
{
    public RepositoryRootArgument() : base("repository-root")
    {
        Description = "Path to the repository root";
        Arity = ArgumentArity.ZeroOrOne;
        DefaultValueFactory = GetDefaultValue;
    }

    private static string GetDefaultValue(ArgumentResult _) => ".";
}