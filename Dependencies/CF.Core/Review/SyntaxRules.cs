using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.Core.Review;

/// <summary>
/// Applies the rules that need nothing but the syntax of a file to the artifacts read from it.
/// </summary>
internal static class SyntaxRules
{
    public static List<Finding> Evaluate(IReadOnlyList<RuleDefinition> rules, ILanguage language, IReadOnlyList<Artifact> artifacts)
    {
        var findings = new List<Finding>();
        foreach(var rule in rules)
        {
            if(rule.Language == language.Id && !MeaningNeeds.Has(rule, language))
                findings.AddRange(RuleEvaluator.Evaluate(rule, artifacts));
        }

        return findings;
    }
}