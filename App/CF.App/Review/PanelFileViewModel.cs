using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// One file in the panel of a push. A file with findings is a button that brings its line to the top of the sheet; a file
/// without is a plain line that says so, and so is a file whose findings all belong to rules the list is not showing.
/// </summary>
internal sealed partial class PanelFileViewModel : ObservableObject
{
    private readonly Action<FileGroupViewModel>? _show;

    public PanelFileViewModel(string path, FileGroupViewModel? group, Action<FileGroupViewModel>? show, string? notReviewedReason, bool hasOtherRulesFindings = false)
    {
        Path = path;
        Group = group;
        _show = show;
        NotReviewedReason = notReviewedReason;
        HasOtherRulesFindings = hasOtherRulesFindings;
        if(group != null)
        {
            group.PropertyChanged += (_, e) =>
            {
                if(e.PropertyName == nameof(FileGroupViewModel.SpokenCounts))
                    OnPropertyChanged(nameof(SpokenName));
                if(e.PropertyName is nameof(FileGroupViewModel.HasBlock) or nameof(FileGroupViewModel.HasWarn))
                {
                    OnPropertyChanged(nameof(StatusIsBlock));
                    OnPropertyChanged(nameof(StatusIsWarn));
                    OnPropertyChanged(nameof(StatusIsDone));
                }
            };
        }
    }

    public FileGroupViewModel? Group { get; }

    public string Path { get; }

    /// <summary>
    /// Why the file was not reviewed, null for a file that was.
    /// </summary>
    public string? NotReviewedReason { get; }

    /// <summary>
    /// Whether the file has findings that the rule chosen in a folder review leaves out of the list.
    /// </summary>
    public bool HasOtherRulesFindings { get; }

    public bool HasFindings
    {
        get
        {
            return Group != null;
        }
    }

    /// <summary>
    /// The mark at the start of a row with findings: the worst effect among them. A file whose findings are all fixed shows the tick.
    /// </summary>
    public bool StatusIsBlock
    {
        get { return Group is { HasBlock: true }; }
    }

    public bool StatusIsWarn
    {
        get { return Group is { HasBlock: false, HasWarn: true }; }
    }

    public bool StatusIsDone
    {
        get { return IsClean || Group is { HasBlock: false, HasWarn: false }; }
    }

    public bool IsClean
    {
        get
        {
            return Group == null && NotReviewedReason == null && !HasOtherRulesFindings;
        }
    }

    public bool IsOtherRules
    {
        get
        {
            return Group == null && NotReviewedReason == null && HasOtherRulesFindings;
        }
    }

    public bool IsNotReviewed
    {
        get
        {
            return Group == null && NotReviewedReason != null;
        }
    }

    public string SpokenName
    {
        get
        {
            if(Group != null)
                return $"{Path}, {Group.SpokenCounts}. Show this file";

            if(IsOtherRules)
                return $"{Path}, findings of other rules";

            return IsClean ? $"{Path}, no findings" : $"{Path}, not reviewed: {NotReviewedReason}";
        }
    }

    [RelayCommand]
    private void Show()
    {
        if(Group != null)
            _show?.Invoke(Group);
    }
}
