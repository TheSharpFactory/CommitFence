using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules;

/// <summary>
/// The heading of a group of rules: the one place that says where the rules under it come from.
/// </summary>
internal sealed partial class GroupHeadingViewModel : RuleListItemViewModel
{
    private readonly Action _changed;
    private readonly Action? _openFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsExpanded { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial int Count { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsFiltered { get; set; }

    /// <summary>
    /// Whether the count is drawn: not while the group is still being read, and not when there is no file to count rules of.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool ShowsCount { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial string Note { get; set; } = string.Empty;

    /// <summary>
    /// Whether the note is the one that says why no row has a switch, and so stands with the lock.
    /// </summary>
    [ObservableProperty]
    public partial bool NoteHasLock { get; set; }

    /// <summary>
    /// Whether "Open file" is drawn: the group has a file to open.
    /// </summary>
    [ObservableProperty]
    public partial bool HasOpenFile { get; set; }

    private string SpokenSource { get; }

    public GroupHeadingViewModel(GroupSource source, string name, string note, string spokenSource, Action changed, Action? openFile = null)
    {
        _openFile = openFile;
        Source = source;
        Name = name;
        Note = note;
        SpokenSource = spokenSource;
        _changed = changed;
    }

    public GroupSource Source { get; }

    public string Name { get; }

    public bool IsCustom
    {
        get
        {
            return Source == GroupSource.Custom;
        }
    }

    public bool IsEditorConfig
    {
        get
        {
            return Source == GroupSource.EditorConfig;
        }
    }

    /// <summary>
    /// What a screen reader says: the name, the count, the note and whether the group is open.
    /// </summary>
    public string SpokenName
    {
        get
        {
            var summary = !ShowsCount ? SpokenSource : IsFiltered ? $"{Count} of {TotalCount} rules shown" : CountWords;
            var state = IsExpanded ? "expanded" : "collapsed";
            return $"{Name}, {summary}, {state}";
        }
    }

    private string CountWords
    {
        get
        {
            return Count == 1 ? "1 rule" : $"{Count} rules";
        }
    }

    public void Fold()
    {
        SetExpanded(false);
    }

    public void Open()
    {
        SetExpanded(true);
    }

    public void Toggle()
    {
        SetExpanded(!IsExpanded);
    }

    [RelayCommand]
    private void OpenFile()
    {
        _openFile?.Invoke();
    }

    [RelayCommand]
    private void ToggleFold()
    {
        Toggle();
    }

    private void SetExpanded(bool expanded)
    {
        if(IsExpanded == expanded)
            return;

        IsExpanded = expanded;
        _changed();
    }
}
