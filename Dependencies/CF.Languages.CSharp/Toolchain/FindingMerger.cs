using CF.Core.Findings;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The same finding from two target frameworks (or two projects that compile one file) is reported once, naming all of
/// them.
/// </summary>
internal static class FindingMerger
{
    public static IReadOnlyList<Finding> Merge(IReadOnlyList<Finding> findings)
    {
        var groups = findings.GroupBy(finding => (finding.RuleId, finding.File, finding.Line, finding.Column, finding.Message));
        return groups.Select(MergeGroup).ToList();
    }

    private static Finding MergeGroup(IGrouping<(string RuleId, string? File, int? Line, int? Column, string Message), Finding> group)
    {
        var first = group.First();
        var projects = JoinDistinct(group.Select(finding => finding.Project));
        var frameworks = JoinDistinct(group.Select(finding => finding.TargetFramework));

        return first.HoldingFor(projects, frameworks);
    }

    private static string? JoinDistinct(IEnumerable<string?> values)
    {
        var distinct = values.OfType<string>().Distinct().ToList();
        return distinct.Count == 0 ? null : string.Join(", ", distinct);
    }
}