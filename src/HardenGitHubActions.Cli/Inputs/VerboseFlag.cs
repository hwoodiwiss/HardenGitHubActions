using System.CommandLine;
using System.CommandLine.Parsing;

namespace HardenGitHubActions.Cli.Inputs;

public sealed class VerboseFlag : Option<bool>
{
    public VerboseFlag() : base("--verbose", "-v")
    {
        Description = "Enable verbose (Debug) logging";
        Arity = ArgumentArity.ZeroOrOne;
        DefaultValueFactory = GetDefaultValue;
    }

    private static bool GetDefaultValue(ArgumentResult _) => false;
}