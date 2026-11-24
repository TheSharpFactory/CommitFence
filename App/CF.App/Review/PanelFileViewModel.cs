using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// One file in the panel of a push. A file with findings is a button that brings its line to the top of the sheet; a file
/// without is a plain line that says so.
/// </summary>
internal sealed partial class PanelFileViewModel
{
    private readonly FileGroupViewModel? _group;
    private readonly Action<FileGroupViewModel>? _show;

    public PanelFileViewModel(string path, FileGroupViewModel? group, Action<FileGroupViewModel>? show, string notReviewedReason)
    {
        Path = path;
        _group = group;
        _show = show;
        NotReviewedReason = notReviewedReason;
    }

    public string Path { get; }

    /// <summary>
    /// Why the file was not reviewed, empty for a file that was.
    /// </summary>
    public string NotReviewedReason { get; }

    public bool HasFindings
    {
        get
        {
            return _group != null;
        }
    }

    public bool IsClean
    {
        get
        {
            return _group == null && NotReviewedReason.Length == 0;
        }
    }

    public bool IsNotReviewed
    {
        get
        {
            return _group == null && NotReviewedReason.Length > 0;
        }
    }

    public bool HasBlock
    {
        get
        {
            return _group?.HasBlock == true;
        }
    }

    public bool HasWarn
    {
        get
        {
            return _group?.HasWarn == true;
        }
    }

    public string BlockText
    {
        get
        {
            return _group?.BlockText ?? string.Empty;
        }
    }

    public string WarnText
    {
        get
        {
            return _group?.WarnText ?? string.Empty;
        }
    }

    public string SpokenName
    {
        get
        {
            if(_group != null)
                return $"{Path}, {_group.BlockCount} blocking, {_group.WarnCount} warning. Show this file";

            return IsClean ? $"{Path}, no findings" : $"{Path}, not reviewed: {NotReviewedReason}";
        }
    }

    [RelayCommand]
    private void Show()
    {
        if(_group != null)
            _show?.Invoke(_group);
    }
}
