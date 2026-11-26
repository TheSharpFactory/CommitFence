using CF.Core.Findings;
using CF.Core.Prompts;
using CF.Core.Review;

namespace CF.Cli.Console;

/// <summary>Writes a prompt using the rules and source from the completed review.</summary>
internal sealed class PromptReporter(TextWriter output)
{
    public void Write(ReviewResult result)
    {
        var prompt = AgentPrompt.ForReview(result.Findings, result.SourceOf, finding => Describe(result, finding), result.Outcome == ReviewOutcome.Incomplete, result.Coverage);
        output.Write(prompt.Replace("\n", Environment.NewLine) + Environment.NewLine);
        if(result.IsQuick)
        {
            foreach(var skipped in result.Skipped)
                output.WriteLine(skipped);
        }
    }

    private static PromptRule? Describe(ReviewResult result, Finding finding)
    {
        var rule = result.RuleOf(finding);
        if(rule?.Team != null)
            return PromptRule.Of(rule.Team);

        return rule?.File == null ? null : PromptRule.Of(rule.File);
    }
}
