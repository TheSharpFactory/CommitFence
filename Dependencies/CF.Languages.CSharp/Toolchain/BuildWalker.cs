using CF.Core.Findings;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Walks the units in dependency order and applies the rules about evidence (no stale evidence).
/// </summary>
/// <remarks>
/// A unit is built only when every unit it depends on succeeded in this same review: an exit code of 0 from a build
/// against a stale copy of a failed project is not evidence. Completeness is judged against the plan, not against the
/// logs found: every unit under review gets a coverage entry, reviewed or not.
/// </remarks>
internal sealed class BuildWalker
{
    private readonly UnitBuilder _builder;
    private readonly UnitGraph _graph;

    public BuildWalker(UnitBuilder builder, UnitGraph graph)
    {
        _builder = builder;
        _graph = graph;
    }

    public void Walk(BuildPlan plan, IReadOnlyDictionary<string, string> restoreFailures, ToolchainReport report)
    {
        var succeeded = new HashSet<string>();
        foreach(var unit in plan.Order)
        {
            var underReview = plan.ReviewedKeys.Contains(unit.Key);
            var outcome = FindReasonNotToBuild(unit, succeeded, restoreFailures) ?? _builder.Build(unit, underReview, plan.OwnerKeys.Contains(unit.Key));
            if(outcome.Succeeded)
                succeeded.Add(unit.Key);

            Record(unit, underReview, outcome, report);
        }
    }

    private UnitOutcome? FindReasonNotToBuild(ProjectUnit unit, HashSet<string> succeeded, IReadOnlyDictionary<string, string> restoreFailures)
    {
        if(restoreFailures.TryGetValue(unit.Project.Path, out var restoreReason))
            return UnitOutcome.NoEvidence(restoreReason);

        var unreadable = _graph.UnreadablePrerequisitesOf(unit).FirstOrDefault();
        if(unreadable != null)
            return UnitOutcome.NoEvidence($"its prerequisite {unreadable.Project.DisplayName} could not be evaluated, so nothing can be said about it");

        var failed = _graph.PrerequisitesOf(unit).FirstOrDefault(prerequisite => !succeeded.Contains(prerequisite.Key));
        if(failed != null)
            return UnitOutcome.NoEvidence($"its prerequisite {failed.Label} did not succeed in this review, so its result would rest on a stale copy");

        return null;
    }

    private static void Record(ProjectUnit unit, bool underReview, UnitOutcome outcome, ToolchainReport report)
    {
        if(underReview)
        {
            report.AddFindings(outcome.Findings);
            report.AddCoverage(unit.Label, outcome.Status, outcome.Reason);
            report.AddMeaning(unit.Label, outcome);
        }

        if(!underReview && !outcome.Succeeded)
            report.AddCoverage(unit.Label, CoverageStatus.Failed, outcome.Reason);
    }
}