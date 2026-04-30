using System.CommandLine;
using System.CommandLine.Parsing;

namespace HardenGitHubActions.Cli.Inputs;

public sealed class QuietFlag : Option<bool>
{
    public QuietFlag() : base("--quiet", "-q")
    {
        Description = "Suppress informational output (Warnings and above only)";
        Arity = ArgumentArity.ZeroOrOne;
        DefaultValueFactory = GetDefaultValue;
    }

    private static bool GetDefaultValue(ArgumentResult _) => false;
}