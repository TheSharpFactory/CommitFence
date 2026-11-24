using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Structure;

namespace CF.Core.Rules;

/// <summary>
/// Runs a code rule on artifacts and returns the findings. It knows nothing of any language: it only compares
/// properties.
/// </summary>
public static class RuleEvaluator
{
    /// <summary>
    /// A finding for each artifact the rule looks at that breaks it. An artifact lacking a property the rule asks about
    /// is a bug of its reader and throws.
    /// </summary>
    public static IReadOnlyList<Finding> Evaluate(RuleDefinition rule, IReadOnlyList<Artifact> artifacts)
    {
        var findings = new List<Finding>();
        foreach(var artifact in artifacts)
        {
            if(IsLookedAt(rule, artifact) && IsBroken(rule, artifact))
                findings.Add(CreateFinding(rule, artifact));
        }

        return findings;
    }

    private static bool IsLookedAt(RuleDefinition rule, Artifact artifact)
    {
        if(artifact.Language != rule.Language || artifact.Type != rule.ArtifactType)
            return false;

        if(!PathFilter.Matches(rule.Paths, artifact.File))
            return false;

        return AllHold(rule.Where, artifact);
    }

    private static bool IsBroken(RuleDefinition rule, Artifact artifact)
    {
        if(rule.Forbid)
            return true;

        return !AllHold(rule.Require, artifact);
    }

    private static bool AllHold(IReadOnlyList<RuleCondition> conditions, Artifact artifact)
    {
        foreach(var condition in conditions)
        {
            if(!condition.IsMetBy(ValueOf(condition.PropertyName, artifact)))
                return false;
        }

        return true;
    }

    internal static PropertyValue ValueOf(string propertyName, Artifact artifact)
    {
        var property = artifact.FindProperty(propertyName);
        if(property == null)
            throw new InvalidOperationException($"The {artifact.Type} at {artifact.File}({artifact.Span.StartLine},{artifact.Span.StartColumn}) has no property '{propertyName}'; its reader must supply every property of its catalog.");

        return property.Value;
    }

    private static Finding CreateFinding(RuleDefinition rule, Artifact artifact)
    {
        var message = rule.Message ?? rule.Title;
        return new Finding(
            rule.Id,
            FindingSource.CodeRule,
            rule.Severity,
            artifact.Language,
            null,
            null,
            artifact.File,
            artifact.Span.StartLine,
            artifact.Span.StartColumn,
            message,
            rule.Why,
            artifact.Reach);
    }
}