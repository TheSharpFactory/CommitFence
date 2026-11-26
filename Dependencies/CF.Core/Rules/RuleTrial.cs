using CF.Core.Code;
using CF.Core.Structure;

namespace CF.Core.Rules;

/// <summary>
/// Judges each artifact of a rule's type in some code: the rule breaks it, follows it, or does not cover it, with the
/// reason in words.
/// </summary>
internal static class RuleTrial
{
    public static IReadOnlyList<ArtifactVerdict> Judge(RuleDefinition rule, IReadOnlyList<Artifact> artifacts, string? fileName)
    {
        var verdicts = new List<ArtifactVerdict>();
        foreach(var artifact in artifacts)
        {
            if(artifact.Type == rule.ArtifactType)
                verdicts.Add(JudgeArtifact(rule, artifact, fileName));
        }

        return verdicts;
    }

    private static ArtifactVerdict JudgeArtifact(RuleDefinition rule, Artifact artifact, string? fileName)
    {
        if(fileName != null && !PathFilter.Matches(rule.Paths, fileName))
            return new ArtifactVerdict(TryOutcome.NotCovered, artifact.Span, "the file is outside the paths of the rule");

        var leftOut = FindFailure(rule.Where, artifact, "the rule looks only at");
        if(leftOut != null)
            return new ArtifactVerdict(TryOutcome.NotCovered, artifact.Span, leftOut);

        if(rule.Forbid)
            return new ArtifactVerdict(TryOutcome.BreaksIt, artifact.Span, "the rule forbids this " + rule.ArtifactType);

        var broken = FindFailure(rule.Require, artifact, "the rule requires");
        if(broken != null)
            return new ArtifactVerdict(TryOutcome.BreaksIt, artifact.Span, broken);

        return new ArtifactVerdict(TryOutcome.FollowsIt, artifact.Span, "every required condition holds");
    }

    private static string? FindFailure(IReadOnlyList<RuleCondition> conditions, Artifact artifact, string verb)
    {
        foreach(var condition in conditions)
        {
            var actual = RuleEvaluator.ValueOf(condition.PropertyName, artifact);
            if(!condition.IsMetBy(actual))
                return $"{condition.PropertyName} is {actual}, {verb} {Describe(condition)}";
        }

        return null;
    }

    private static string Describe(RuleCondition condition)
    {
        var values = string.Join(", ", condition.ExpectedValues);
        return condition.Comparison switch
        {
            Comparison.Is => values,
            Comparison.IsOneOf => "one of " + values,
            Comparison.IsNot or Comparison.AtMost or Comparison.AtLeast or Comparison.Matches => "that " + RuleSentence.Condition(condition),
            _ => values
        };
    }
}