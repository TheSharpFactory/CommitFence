using CF.Core.Code;
using CF.Core.Findings;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// What a severity word of the .editorconfig means for the build and for a push, as the real builds showed: error blocks,
/// warning and suggestion warn (the review turns a suggestion into a warning), silent and none switch the rule off, and
/// default leaves the choice to the rule. Case does not matter.
/// </summary>
internal static class SeverityWords
{
    public static FileRuleState StateOf(string word)
    {
        switch(word.ToLowerInvariant())
        {
            case "error":
            case "warning":
            case "suggestion":
                return FileRuleState.On;
            case "silent":
            case "none":
                return FileRuleState.Off;
            case "default":
                return FileRuleState.OwnDefault;
            default:
                return FileRuleState.NotUnderstood;
        }
    }

    public static Severity? SeverityOf(string word)
    {
        if(StateOf(word) != FileRuleState.On)
            return null;

        return word.Equals("error", StringComparison.OrdinalIgnoreCase) ? Severity.Block : Severity.Warn;
    }
}
