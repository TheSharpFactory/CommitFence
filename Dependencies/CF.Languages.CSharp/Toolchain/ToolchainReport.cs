using CF.Core.Code;
using CF.Core.Findings;
using CF.Languages.CSharp.Meaning;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Collects what one run of the toolchain finds, covers and learns. Whatever is not reviewed is also a gap in the
/// meaning: answers with a gap are never judged.
/// </summary>
internal sealed class ToolchainReport
{
    private readonly List<Finding> _findings = new List<Finding>();
    private readonly List<Coverage> _coverage = new List<Coverage>();
    private readonly List<string> _notes = new List<string>();
    private readonly List<FileReading> _readings = new List<FileReading>();
    private readonly List<UnitFacts> _facts = new List<UnitFacts>();
    private readonly List<string> _gaps = new List<string>();
    private IReadOnlyList<string> _treeAssemblies = new List<string>();

    public bool RanBuild { get; set; }

    /// <summary>
    /// The facts of the units that answered, for keeping them.
    /// </summary>
    public IReadOnlyList<UnitFacts> Facts
    {
        get { return _facts; }
    }

    public void AddFindings(IEnumerable<Finding> findings)
    {
        _findings.AddRange(findings);
    }

    public void AddCoverage(string what, CoverageStatus status, string reason)
    {
        _coverage.Add(new Coverage(what, status, reason, CoverageKind.Build));
        if(status != CoverageStatus.Reviewed)
            AddGap($"{what}: {reason}");
    }

    public void AddNote(string note)
    {
        _notes.Add(note);
    }

    public void AddReadings(IEnumerable<FileReading> readings)
    {
        _readings.AddRange(readings);
    }

    public void AddGap(string gap)
    {
        if(!_gaps.Contains(gap))
            _gaps.Add(gap);
    }

    /// <summary>
    /// What a unit under review said about the meaning of its code.
    /// </summary>
    /// <remarks>
    /// A unit that was not reviewed already has its gap through its coverage entry; one that compiled with errors
    /// answered, but not about code that compiles.
    /// </remarks>
    public void AddMeaning(string label, UnitOutcome outcome)
    {
        if(outcome.Facts != null)
            _facts.Add(outcome.Facts);
        else if(outcome.Status == CoverageStatus.Reviewed)
            AddGap($"{label}: {outcome.Reason}");
    }

    public ToolchainResult ToResult()
    {
        return new ToolchainResult(FindingMerger.Merge(_findings), _coverage, _notes, ToAnswers(), _readings) { RanBuild = RanBuild };
    }

    public MeaningAnswers ToAnswers()
    {
        return MeaningAnswerBuilder.Build(_facts, _gaps, _treeAssemblies);
    }

    /// <summary>
    /// The assemblies of every project of the tree, so that a use of a type of a project that was not compiled is told
    /// from a use of a library.
    /// </summary>
    public void SetTreeAssemblies(IEnumerable<string> assemblies)
    {
        _treeAssemblies = assemblies.ToList();
    }
}