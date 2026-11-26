using CF.App.Rules.Page;

namespace CF.App.Review;

/// <summary>
/// What the verdict place shows for one state: the heading, the count pieces, the facts and, for the states that have no
/// badge, the lead line.
/// </summary>
internal sealed class VerdictViewModel
{
    private static readonly List<CountPieceViewModel> _noCounts = new List<CountPieceViewModel>();
    private static readonly List<FactViewModel> _noFacts = new List<FactViewModel>();

    public VerdictViewModel(VerdictKind kind, string heading, string lead, IReadOnlyList<CountPieceViewModel> counts, IReadOnlyList<FactViewModel> facts)
    {
        Kind = kind;
        Heading = heading;
        Lead = lead;
        Counts = counts;
        Facts = facts;
    }

    public VerdictKind Kind { get; }

    public string Heading { get; }

    public string Lead { get; }

    /// <summary>
    /// The second piece of the heading of a folder review: "in Billing".
    /// </summary>
    public string HeadingTail { get; init; } = string.Empty;

    /// <summary>
    /// Whether the line that says what the review leaves out ("Uncommitted changes are not part of this review.") stands under the facts.
    /// </summary>
    public bool LeavesOutUncommitted { get; init; }

    /// <summary>
    /// What that line says: the uncommitted work for a folder, the unstaged changes for a review of the staged changes.
    /// </summary>
    public string LeavesOutText { get; init; } = "Uncommitted changes are not part of this review.";

    /// <summary>
    /// A line of content under the lead of a block before a review: a fact, with the icon of a file.
    /// </summary>
    public string ContentLine { get; init; } = string.Empty;

    /// <summary>
    /// The words of the lead drawn in the code face, such as git add, which stand inside the lead; empty when there are none.
    /// </summary>
    public string LeadCode { get; init; } = string.Empty;

    /// <summary>
    /// What to do next, under the content line: "Stage what belongs in the next commit, then check again.".
    /// </summary>
    public string AfterLine { get; init; } = string.Empty;

    public bool HasAfterLine
    {
        get { return AfterLine.Length > 0; }
    }

    /// <summary>
    /// The files that are staged and not reviewed yet, as the ready block lists them (the first few).
    /// </summary>
    public IReadOnlyList<PathFileLine> StagedFiles { get; init; } = new List<PathFileLine>();

    /// <summary>
    /// "and 5 more" when the staged files are more than the block lists.
    /// </summary>
    public string MoreFiles { get; init; } = string.Empty;

    /// <summary>
    /// The files the list under the head names: the staged ones when files are staged, the changed ones when none is.
    /// </summary>
    public IReadOnlyList<ListedFile> FileRows { get; init; } = new List<ListedFile>();

    /// <summary>
    /// The label over that list.
    /// </summary>
    public string FileListHeading { get; init; } = string.Empty;

    public bool HasFileRows
    {
        get { return FileRows.Count > 0; }
    }

    /// <summary>
    /// A state before a review of the staged changes: it takes the head of a result, with its tile, its title and its one action.
    /// </summary>
    public bool ShowsResultHead
    {
        get { return Kind is VerdictKind.StagedReady or VerdictKind.NothingStaged or VerdictKind.StagedUnreadable; }
    }

    /// <summary>
    /// The index of the staged changes could not be read: the tile is the red one and carries the alert.
    /// </summary>
    public bool IsUnreadable
    {
        get { return Kind == VerdictKind.StagedUnreadable; }
    }

    /// <summary>
    /// The review of the staged changes found something that would block a push.
    /// </summary>
    public bool HasBlocking { get; init; }

    /// <summary>
    /// A finished review of the staged changes: its tile keeps the glyph of the staged changes and takes the verdict's colour.
    /// </summary>
    public bool IsStagedResult
    {
        get { return Kind is VerdictKind.StagedFindings or VerdictKind.StagedClean; }
    }

    public bool ShowsBlockTile
    {
        get { return IsRefused || (IsStagedFindings && HasBlocking); }
    }

    public bool ShowsWarnTile
    {
        get { return IsStagedFindings && !HasBlocking; }
    }

    public bool IsAllowedTile
    {
        get { return IsAllowed; }
    }

    public bool ShowsBlockStagedGlyph
    {
        get { return IsStagedFindings && HasBlocking; }
    }

    public bool IsStagedClean
    {
        get { return Kind == VerdictKind.StagedClean; }
    }

    public bool ShowsCheckTile
    {
        get { return IsAllowed && !IsStagedResult; }
    }

    public bool HasStagedFiles
    {
        get { return StagedFiles.Count > 0; }
    }

    public bool HasMoreFiles
    {
        get { return MoreFiles.Length > 0; }
    }

    /// <summary>
    /// Whether the line that says what the review leaves out is a quiet fact (the staged changes) and not a strip.
    /// </summary>
    public bool LeavesOutQuiet { get; init; }

    public bool LeavesOutAsStrip
    {
        get { return LeavesOutUncommitted && !LeavesOutQuiet; }
    }

    public bool LeavesOutAsFact
    {
        get { return LeavesOutUncommitted && LeavesOutQuiet; }
    }

    /// <summary>
    /// The index of the staged changes could not be read: the block offers "Try again" in place of a review.
    /// </summary>
    public bool ShowsTryAgain { get; init; }

    public bool ShowsCheckAgain
    {
        get { return Kind == VerdictKind.NothingStaged; }
    }

    /// <summary>
    /// What git said when the index could not be read, as the first sentence of the body, which is the red one; empty otherwise.
    /// </summary>
    public string ProblemFirst { get; init; } = string.Empty;

    /// <summary>
    /// What to do about it, in the rest of the body.
    /// </summary>
    public string ProblemRest { get; init; } = string.Empty;

    public bool HasProblem
    {
        get { return ProblemFirst.Length > 0; }
    }

    public string ProblemSpoken
    {
        get { return $"{ProblemFirst} {ProblemRest}"; }
    }

    public bool HasContentLine
    {
        get { return ContentLine.Length > 0; }
    }

    public string LeadBefore
    {
        get { return LeadCode.Length == 0 ? Lead : Lead[..Lead.IndexOf(LeadCode, StringComparison.Ordinal)]; }
    }

    public string LeadAfter
    {
        get { return LeadCode.Length == 0 ? string.Empty : Lead[(Lead.IndexOf(LeadCode, StringComparison.Ordinal) + LeadCode.Length)..]; }
    }

    public bool HasTail
    {
        get
        {
            return HeadingTail.Length > 0;
        }
    }

    /// <summary>
    /// The block before a review: nothing reviewed yet, for a push or for a folder. It holds the logo and the run button.
    /// </summary>
    public bool HasEmblem
    {
        get
        {
            return Kind is VerdictKind.NoReview or VerdictKind.StagedReady or VerdictKind.NothingStaged;
        }
    }

    public bool HasButton
    {
        get
        {
            return Kind is VerdictKind.NoReview or VerdictKind.StagedReady;
        }
    }

    public bool IsFolderFindings
    {
        get
        {
            return Kind == VerdictKind.FolderFindings;
        }
    }

    /// <summary>
    /// Findings that count but refuse nothing, in a folder or in the staged changes: the solid badge.
    /// </summary>
    public bool IsCountedFindings
    {
        get
        {
            return Kind is VerdictKind.FolderFindings or VerdictKind.StagedFindings;
        }
    }

    public bool IsStagedFindings
    {
        get
        {
            return Kind == VerdictKind.StagedFindings;
        }
    }

    public IReadOnlyList<CountPieceViewModel> Counts { get; }

    public IReadOnlyList<FactViewModel> Facts { get; }

    public bool HasLead
    {
        get
        {
            return Lead.Length > 0;
        }
    }

    public bool HasCounts
    {
        get
        {
            return Counts.Count > 0;
        }
    }

    public bool HasFacts
    {
        get
        {
            return Facts.Count > 0;
        }
    }

    public bool HasBadge
    {
        get
        {
            return Kind is VerdictKind.Running or VerdictKind.Refused or VerdictKind.AllowedWithWarnings or VerdictKind.AllowedClean or VerdictKind.Incomplete or VerdictKind.FolderFindings or VerdictKind.FolderClean or VerdictKind.StagedFindings or VerdictKind.StagedClean;
        }
    }

    public bool IsRefused
    {
        get
        {
            return Kind == VerdictKind.Refused;
        }
    }

    public bool IsAllowed
    {
        get
        {
            return Kind is VerdictKind.AllowedWithWarnings or VerdictKind.AllowedClean or VerdictKind.FolderClean or VerdictKind.StagedClean;
        }
    }

    public bool IsIncomplete
    {
        get
        {
            return Kind == VerdictKind.Incomplete;
        }
    }

    public bool IsRunning
    {
        get
        {
            return Kind == VerdictKind.Running;
        }
    }

    public static VerdictViewModel Blank(VerdictKind kind, string heading, string lead)
    {
        return new VerdictViewModel(kind, heading, lead, _noCounts, _noFacts);
    }

    public static VerdictViewModel Running()
    {
        return new VerdictViewModel(VerdictKind.Running, "Reviewing", string.Empty, _noCounts, _noFacts);
    }
}
