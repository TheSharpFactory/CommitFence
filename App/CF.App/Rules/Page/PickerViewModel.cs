using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The list a token of the sentence opens: entries, in groups when the caller says so,
/// a row to find an entry when there are more than eight, a highlight the keys move, and the choice.
/// </summary>
internal sealed partial class PickerViewModel : ObservableObject
{
    private const int _findFrom = 9;

    private readonly List<PickerEntryViewModel> _entries = new List<PickerEntryViewModel>();

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string FindText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowsNoMatch { get; set; }

    public PickerViewModel(string spokenName, double width, string findPlaceholder, string noMatchText)
    {
        SpokenName = spokenName;
        Width = width;
        FindPlaceholder = findPlaceholder;
        NoMatchText = noMatchText;
    }

    /// <summary>
    /// Raised when an entry is chosen, after the picker closed.
    /// </summary>
    public event Action<PickerEntryViewModel>? Chosen;

    public ObservableCollection<PickerItemViewModel> Items { get; } = new ObservableCollection<PickerItemViewModel>();

    public string SpokenName { get; }

    public double Width { get; }

    public string FindPlaceholder { get; }

    public string NoMatchText { get; }

    public bool HasFindRow { get; private set; }

    public bool AlwaysFind { get; init; }

    /// <summary>
    /// Opens the picker on these entries. The highlight starts on the current entry when the keyboard opened it.
    /// </summary>
    public void Open(IReadOnlyList<PickerEntryViewModel> entries, bool byKeyboard)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        HasFindRow = AlwaysFind || _entries.Count >= _findFrom;
        OnPropertyChanged(nameof(HasFindRow));
        foreach(var entry in _entries)
            entry.IsHighlighted = byKeyboard && entry.IsCurrent && !entry.IsDisabled;

        FindText = string.Empty;
        Refilter();
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }

    /// <summary>
    /// Moves the highlight over the entries that can be chosen; it stops at the ends.
    /// </summary>
    public void MoveHighlight(int step)
    {
        var shown = Items.OfType<PickerEntryViewModel>().Where(entry => !entry.IsDisabled).ToList();
        if(shown.Count == 0)
            return;

        var current = shown.FindIndex(entry => entry.IsHighlighted);
        var target = step == int.MinValue ? 0 : step == int.MaxValue ? shown.Count - 1 : Math.Clamp(current + step, 0, shown.Count - 1);
        foreach(var entry in _entries)
            entry.IsHighlighted = false;

        shown[target].IsHighlighted = true;
    }

    public void ChooseHighlighted()
    {
        var entry = Items.OfType<PickerEntryViewModel>().FirstOrDefault(candidate => candidate.IsHighlighted);
        if(entry != null)
            Choose(entry);
        else if(Items.OfType<PickerEntryViewModel>().Count(candidate => !candidate.IsDisabled) == 1 && FindText.Length > 0)
            Choose(Items.OfType<PickerEntryViewModel>().First(candidate => !candidate.IsDisabled));
    }

    partial void OnFindTextChanged(string value)
    {
        Refilter();
    }

    [RelayCommand]
    private void Choose(PickerEntryViewModel entry)
    {
        if(entry.IsDisabled)
            return;

        IsOpen = false;
        Chosen?.Invoke(entry);
    }

    private void Refilter()
    {
        Items.Clear();
        var text = FindText.Trim();
        var lastGroup = string.Empty;
        foreach(var entry in _entries.Where(candidate => text.Length == 0 || candidate.Name.Contains(text, StringComparison.OrdinalIgnoreCase)))
        {
            if(entry.Group.Length > 0 && entry.Group != lastGroup)
                Items.Add(new PickerGroupLabel(entry.Group));

            lastGroup = entry.Group;
            Items.Add(entry);
        }

        ShowsNoMatch = Items.Count == 0;
    }
}
