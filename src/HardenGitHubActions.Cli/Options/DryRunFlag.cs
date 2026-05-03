using System.CommandLine;
using System.CommandLine.Parsing;

namespace HardenGitHubActions.Cli.Options;

public sealed class DryRunFlag : Option<bool>
{
    public DryRunFlag() : base("--dry-run")
    {
        Description = "Show what would change without writing any files";
        Arity = ArgumentArity.ZeroOrOne;
        DefaultValueFactory = GetDefaultValue;
    }

    private static bool GetDefaultValue(ArgumentResult _) => false;
}