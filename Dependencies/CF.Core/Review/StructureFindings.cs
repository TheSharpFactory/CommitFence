using CF.Core.Findings;
using CF.Core.Structure;

namespace CF.Core.Review;

/// <summary>
/// Turns a violation into the finding a person reads: at the place of the use, blocking, saying who uses what.
/// </summary>
internal static class StructureFindings
{
    public static Finding Create(StructureViolation violation)
    {
        var use = violation.Use;
        var user = use.Member.Length == 0 ? "Code outside any member" : use.Member;
        var message = $"Layer direction: {violation.FromModule} must not use {violation.ToModule}. {user} uses {use.UsedType} ({use.UsedAssembly}).";

        return new Finding(
            StructureRuleInfo.LayerDirectionId,
            FindingSource.StructureRule,
            Severity.Block,
            violation.Language,
            null,
            null,
            use.File,
            violation.Place.Line,
            violation.Place.Column,
            message,
            null,
            null);
    }
}