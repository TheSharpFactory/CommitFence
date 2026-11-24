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
    /// Whether the line "Uncommitted changes are not part of this review." stands under the facts.
    /// </summary>
    public bool LeavesOutUncommitted { get; init; }

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
            return Kind == VerdictKind.NoReview;
        }
    }

    public bool HasButton
    {
        get
        {
            return Kind == VerdictKind.NoReview;
        }
    }

    public bool IsFolderFindings
    {
        get
        {
            return Kind == VerdictKind.FolderFindings;
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
            return Kind is VerdictKind.Running or VerdictKind.Refused or VerdictKind.AllowedWithWarnings or VerdictKind.AllowedClean or VerdictKind.Incomplete or VerdictKind.FolderFindings or VerdictKind.FolderClean;
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
            return Kind is VerdictKind.AllowedWithWarnings or VerdictKind.AllowedClean or VerdictKind.FolderClean;
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
