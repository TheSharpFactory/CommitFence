using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// One file in the panel of a push. A file with findings is a button that brings its line to the top of the sheet; a file
/// without is a plain line that says so.
/// </summary>
internal sealed partial class PanelFileViewModel : ObservableObject
{
    private readonly Action<FileGroupViewModel>? _show;

    public PanelFileViewModel(string path, FileGroupViewModel? group, Action<FileGroupViewModel>? show, string notReviewedReason)
    {
        Path = path;
        Group = group;
        _show = show;
        NotReviewedReason = notReviewedReason;
        if(group != null)
        {
            group.PropertyChanged += (_, e) =>
            {
                if(e.PropertyName == nameof(FileGroupViewModel.SpokenCounts))
                    OnPropertyChanged(nameof(SpokenName));
            };
        }
    }

    public FileGroupViewModel? Group { get; }

    public string Path { get; }

    /// <summary>
    /// Why the file was not reviewed, empty for a file that was.
    /// </summary>
    public string NotReviewedReason { get; }

    public bool HasFindings
    {
        get
        {
            return Group != null;
        }
    }

    public bool IsClean
    {
        get
        {
            return Group == null && NotReviewedReason.Length == 0;
        }
    }

    public bool IsNotReviewed
    {
        get
        {
            return Group == null && NotReviewedReason.Length > 0;
        }
    }

    public string SpokenName
    {
        get
        {
            if(Group != null)
                return $"{Path}, {Group.SpokenCounts}. Show this file";

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
