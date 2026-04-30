using HardenGitHubActions.Core;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace HardenGitHubActions.Cli.Inputs;

public sealed class CommentModeOption : Option<TagCommentMode>
{
    public CommentModeOption() : base("--comment-mode")
    {
        Description = "Append a tag comment after each pinned SHA";
        Arity = ArgumentArity.ZeroOrOne;
        DefaultValueFactory = GetDefaultValue;
        CustomParser = CustomEnumParser;
        AcceptOnlyFromAmong("None", "ExactTag", "MostSpecificTag");
    }

    private static TagCommentMode GetDefaultValue(ArgumentResult _) => TagCommentMode.MostSpecificTag;

    private static TagCommentMode CustomEnumParser(ArgumentResult argResult)
    {
        var token = argResult.Tokens.Count > 0 ? argResult.Tokens[0].Value : string.Empty;
        if (Enum.TryParse<TagCommentMode>(token, ignoreCase: true, out var result))
        {
            return result;
        }

        argResult.AddError($"Invalid value '{token}' for --comment-mode. Valid values are: None, ExactTag, MostSpecificTag.");
        return TagCommentMode.MostSpecificTag; // Return a default value, but it won't be used due to the error.
    }
}