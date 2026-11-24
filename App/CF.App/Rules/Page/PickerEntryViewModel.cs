using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Rules.Page;

/// <summary>
/// One entry of a picker: a name, what kind of value it is (or a note), whether it is the current one, and whether it can
/// be chosen.
/// </summary>
internal sealed partial class PickerEntryViewModel : PickerItemViewModel
{
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    public PickerEntryViewModel(string key, string name, string kindText, string group, bool isCurrent, bool isDisabled)
    {
        Key = key;
        Name = name;
        KindText = kindText;
        Group = group;
        IsCurrent = isCurrent;
        IsDisabled = isDisabled;
    }

    /// <summary>
    /// What the entry stands for in the rule: a property name, an artifact type, a language, require or forbid.
    /// </summary>
    public string Key { get; }

    public string Name { get; }

    /// <summary>
    /// The kind of value ("number", "true or false"), or the note under require and forbid.
    /// </summary>
    public string KindText { get; }

    /// <summary>
    /// The label of the group the entry stands under; empty when the picker has no groups.
    /// </summary>
    public string Group { get; }

    public bool IsDisabled { get; }

    public bool IsEnabled
    {
        get
        {
            return !IsDisabled;
        }
    }

    public bool IsNormal
    {
        get
        {
            return !IsCurrent;
        }
    }
}
