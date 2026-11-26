using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// The head of the code of a finding: the way to open the file at the line, a tag with the state of the copy on disk, then which copy of
/// the file the lines are, in at most two lines, and when the file is gone what to do. The lines stay the reviewed ones; the head says when the copy has moved on, and a box of two number
/// columns shows where the code is on disk.
/// </summary>
internal sealed partial class FindingViewModel
{
    private const string _noEditorTip = "No editor that opens at a line was found.";

    private SourceCopyAnswer? _copy;

    /// <summary>
    /// Where the code comes from and how to open it; nothing for a result that has no files on disk to open.
    /// </summary>
    public CodeOrigin? Origin { get; init; }

    public bool HasCodeHead
    {
        get { return Origin != null && _finding.File != null && _finding.Line != null; }
    }

    private bool IsStagedOrigin
    {
        get { return Origin?.Kind == ReviewKind.Staged; }
    }

    private string EditorName
    {
        get { return Origin?.EditorName() ?? string.Empty; }
    }

    public bool HasEditor
    {
        get { return EditorName.Length > 0; }
    }

    private SourceCopyState CopyState
    {
        get { return _copy?.State ?? SourceCopyState.NotCompared; }
    }

    /// <summary>
    /// The lines are the same on disk, but the rest of the file is not what the review read.
    /// </summary>
    private bool ChangedElsewhere
    {
        get { return _copy?.ChangedElsewhere == true; }
    }

    /// <summary>
    /// The line the button opens: the one the code is found on now when it moved, the top of the file when the code is not found in it
    /// exactly once or the copy was not compared (a guessed line can be another method), else the finding's own.
    /// </summary>
    public int OpenLineNumber
    {
        get { return OpensAtTop ? 1 : _copy?.Line ?? _finding.Line ?? 1; }
    }

    private bool OpensAtTop
    {
        get { return CopyState is SourceCopyState.Lost or SourceCopyState.Ambiguous or SourceCopyState.NotCompared; }
    }

    public string OpenSourceText
    {
        get { return HasEditor && !OpensAtTop ? $"Open at line {OpenLineNumber}" : "Open the file"; }
    }

    public bool CanOpenSource
    {
        get { return HasCodeHead && CopyState != SourceCopyState.Gone; }
    }

    /// <summary>
    /// The head's action while the file can be opened; when it is gone the action is to review again.
    /// </summary>
    public bool ShowsOpenSource
    {
        get { return CopyState != SourceCopyState.Gone; }
    }

    public bool ShowsReviewAgain
    {
        get { return CopyState == SourceCopyState.Gone; }
    }

    public string OpenSourceTip
    {
        get
        {
            if(!HasEditor)
                return _noEditorTip;

            return OpensAtTop ? $"Open {_finding.File} at its top in {EditorName} (Ctrl+O)" : $"Open {_finding.File} at line {OpenLineNumber} in {EditorName} (Ctrl+O)";
        }
    }

    public string OpenSourceSpokenName
    {
        get
        {
            var name = Path.GetFileName(_finding.File ?? string.Empty);
            if(!HasEditor)
                return $"Open {name}";

            return CopyState switch
            {
                SourceCopyState.Found => $"Open {name} at line {OpenLineNumber}, where this code is now",
                SourceCopyState.Lost or SourceCopyState.Ambiguous or SourceCopyState.NotCompared => $"Open {name} at its top in {EditorName}",
                SourceCopyState.Same or SourceCopyState.Gone => $"Open {name} at line {OpenLineNumber} in {EditorName}",
                _ => $"Open {name} at line {OpenLineNumber} in {EditorName}"
            };
        }
    }

    /// <summary>
    /// The state of the copy on disk, as the tag says it: "Same as on disk" only after a comparison that found it the same.
    /// </summary>
    public string CopyTag
    {
        get
        {
            return CopyState switch
            {
                SourceCopyState.Same => "Same as on disk",
                SourceCopyState.Found => $"Moved to line {OpenLineNumber}",
                SourceCopyState.Lost => "Not found on disk",
                SourceCopyState.Ambiguous => "Found more than once on disk",
                SourceCopyState.Gone => "Not on disk",
                SourceCopyState.NotCompared => "Not compared",
                _ => "Not compared"
            };
        }
    }

    public bool TagIsSame
    {
        get { return CopyState == SourceCopyState.Same; }
    }

    public bool TagIsMoved
    {
        get { return CopyState == SourceCopyState.Found; }
    }

    public bool TagIsGone
    {
        get { return CopyState == SourceCopyState.Gone; }
    }

    public bool TagIsLost
    {
        get { return CopyState == SourceCopyState.Lost; }
    }

    public bool TagIsAmbiguous
    {
        get { return CopyState == SourceCopyState.Ambiguous; }
    }

    public bool TagIsNotCompared
    {
        get { return CopyState is SourceCopyState.NotCompared; }
    }

    /// <summary>
    /// What the head says after its tag, with the code in backticks: which copy the lines are, then what the copy on disk did since. It
    /// stands in at most two lines.
    /// </summary>
    public string HeadText
    {
        get { return WhichCopy() + OnDisk(); }
    }

    /// <summary>
    /// What to do when the file is gone, under the head's line and outside its two-line limit; nothing otherwise.
    /// </summary>
    public string GoneText
    {
        get { return CopyState == SourceCopyState.Gone ? WhenGone() : string.Empty; }
    }

    public bool HasGoneText
    {
        get { return CopyState == SourceCopyState.Gone; }
    }

    /// <summary>
    /// The head's whole text as one sentence, with its tag first, for the spoken name and for a test.
    /// </summary>
    public string CopyNote
    {
        get { return $"{CopyTag}. {HeadText}{(HasGoneText ? " " + GoneText : string.Empty)}".Replace("`", string.Empty, StringComparison.Ordinal); }
    }

    /// <summary>
    /// Whether the box shows two number columns: the reviewed copy's and the copy on disk's.
    /// </summary>
    public bool HasTwoNumbers
    {
        get { return HasCodeHead && CopyState == SourceCopyState.Found; }
    }

    /// <summary>
    /// The word over the first number column: which copy the reviewed lines are.
    /// </summary>
    public string FirstNumberHead
    {
        get
        {
            return Origin?.Kind switch
            {
                ReviewKind.Staged => "Staged",
                ReviewKind.Folder => "Committed",
                ReviewKind.Push => "Pushed",
                _ => "Pushed"
            };
        }
    }

    private string WhichCopy()
    {
        var commit = Origin?.Commit ?? string.Empty;
        var hasCommit = commit.Length > 0;
        return Origin?.Kind switch
        {
            ReviewKind.Staged => hasCommit ? $"The staged copy, on top of commit `{commit}`." : "The staged copy, for the first commit.",
            ReviewKind.Folder => hasCommit ? $"The copy in the last commit, `{commit}`." : "The copy in the last commit.",
            ReviewKind.Push => hasCommit ? $"Commit `{commit}` in this push." : "The copy in this push.",
            _ => string.Empty
        };
    }

    private string OnDisk()
    {
        var changed = IsStagedOrigin ? "has changed since you staged it" : "has changed since";
        return CopyState switch
        {
            SourceCopyState.Found => $" Your copy on disk {changed}.",
            SourceCopyState.Lost => $" Your copy on disk {changed}, and this code was not found in it, so the file opens at the top.",
            SourceCopyState.Ambiguous => $" Your copy on disk {changed}, and this code is found in it more than once, so the file opens at the top.",
            SourceCopyState.NotCompared => $" Your copy on disk was not compared: {NotComparedReason()}.",
            SourceCopyState.Same when ChangedElsewhere => " The file changed elsewhere since; these lines did not.",
            SourceCopyState.Same or SourceCopyState.Gone => string.Empty,
            _ => string.Empty
        };
    }

    private string NotComparedReason()
    {
        var reason = (_copy?.Reason ?? SourceCopy.ReviewedCopyMissing).Trim().TrimEnd('.');
        return reason.Length == 0 ? SourceCopy.ReviewedCopyMissing : reason;
    }

    private string WhenGone()
    {
        var name = Path.GetFileName(_finding.File ?? string.Empty);
        return IsStagedOrigin
            ? $"`{name}` is not on disk now, but its staged copy is what the next commit would record. Stage the deletion or restore the file, then review again."
            : $"`{name}` is not on disk now. If you renamed or deleted it, commit that, then review again.";
    }

    /// <summary>
    /// The number of a reviewed line on the copy on disk, or nothing when that line was not matched there. The code was found as one block,
    /// so the lines around it move as it did; a line whose text is not where it should be is left without a number, because it may
    /// have moved on its own as well as gone.
    /// </summary>
    private int? DiskNumberOf(int number)
    {
        if(_copy?.Disk == null || _content == null || _finding.Line == null)
            return null;

        var onDisk = number + _copy.Line - _finding.Line.Value;
        var reviewed = _content.Split('\n');
        if(number < 1 || number > reviewed.Length || onDisk < 1 || onDisk > _copy.Disk.Count)
            return null;

        return string.Equals(reviewed[number - 1].Trim(), _copy.Disk[onDisk - 1], StringComparison.Ordinal) ? onDisk : null;
    }

    private void BuildLines()
    {
        if(_content == null || _finding.Line == null)
            return;

        Lines = CodeAround.Cut(_content, _finding.Line.Value, IsBlock, HasTwoNumbers ? DiskNumberOf : null, FirstNumberHead);
    }

    /// <summary>
    /// Reads the copy on disk against what the review read, and tells every binding of the head. It is read when the finding opens
    /// and again when the file is opened from it, because the file can change between the two.
    /// </summary>
    private void ReadCopy()
    {
        if(!HasCodeHead || Origin == null)
            return;

        var path = Path.Combine(Origin.RepositoryRoot, _finding.File!.Replace('/', Path.DirectorySeparatorChar));
        _copy = SourceCopy.Compare(_content, path, _finding.Line!.Value);
        BuildLines();
        foreach(var name in new[] { nameof(OpenLineNumber), nameof(OpenSourceText), nameof(CanOpenSource), nameof(ShowsOpenSource), nameof(ShowsReviewAgain), nameof(OpenSourceTip), nameof(OpenSourceSpokenName), nameof(CopyTag), nameof(TagIsSame), nameof(TagIsMoved), nameof(TagIsGone), nameof(TagIsLost), nameof(TagIsAmbiguous), nameof(TagIsNotCompared), nameof(GoneText), nameof(HasGoneText), nameof(HeadText), nameof(CopyNote), nameof(HasTwoNumbers), nameof(HasEditor) })
            OnPropertyChanged(name);

        OpenSourceCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOpenSource))]
    private void OpenSource()
    {
        if(Origin == null || _finding.File == null)
            return;

        ReadCopy();
        if(CopyState == SourceCopyState.Gone)
            return;

        var path = Path.Combine(Origin.RepositoryRoot, _finding.File.Replace('/', Path.DirectorySeparatorChar));
        Origin.OpenSource(path, OpenLineNumber);
    }
}
