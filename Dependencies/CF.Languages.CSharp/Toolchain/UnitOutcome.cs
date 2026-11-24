using CF.Core.Findings;
using CF.Languages.CSharp.Meaning;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// What became of building one unit.
/// </summary>
/// <remarks>
/// It says whether the unit succeeded (the only thing a dependent may rely on), what it found, how it is covered, and
/// the facts of the meaning analyzer when its log was closed correctly.
/// </remarks>
internal sealed class UnitOutcome
{
    public UnitOutcome(bool succeeded, CoverageStatus status, string reason, IReadOnlyList<Finding> findings, UnitFacts? facts)
    {
        Succeeded = succeeded;
        Status = status;
        Reason = reason;
        Findings = findings;
        Facts = facts;
    }

    public bool Succeeded { get; }

    public CoverageStatus Status { get; }

    public string Reason { get; }

    public IReadOnlyList<Finding> Findings { get; }

    public UnitFacts? Facts { get; }

    /// <summary>
    /// The same outcome with more findings, which come first.
    /// </summary>
    public UnitOutcome WithMoreFindings(IReadOnlyList<Finding> more)
    {
        return new UnitOutcome(Succeeded, Status, Reason, more.Concat(Findings).ToList(), Facts);
    }

    /// <summary>
    /// The same outcome with a reason: how it is covered, in words, although it succeeded.
    /// </summary>
    public UnitOutcome WithReason(string reason)
    {
        return new UnitOutcome(Succeeded, Status, reason, Findings, Facts);
    }

    public static UnitOutcome Fine(IReadOnlyList<Finding> findings, UnitFacts? facts)
    {
        return new UnitOutcome(true, CoverageStatus.Reviewed, string.Empty, findings, facts);
    }

    public static UnitOutcome Broken(string reason, IReadOnlyList<Finding> findings)
    {
        return new UnitOutcome(false, CoverageStatus.Reviewed, reason, findings, null);
    }

    public static UnitOutcome NoEvidence(string reason)
    {
        return NoEvidence(reason, new List<Finding>());
    }

    public static UnitOutcome NoEvidence(string reason, IReadOnlyList<Finding> findings)
    {
        return new UnitOutcome(false, CoverageStatus.Failed, reason, findings, null);
    }
}