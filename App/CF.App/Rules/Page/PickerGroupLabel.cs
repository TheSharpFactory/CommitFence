namespace CF.App.Rules.Page;

/// <summary>
/// The label of a group of entries in a picker: "Read from the file: runs in quick mode".
/// </summary>
internal sealed class PickerGroupLabel : PickerItemViewModel
{
    public PickerGroupLabel(string text)
    {
        Text = text;
    }

    public string Text { get; }
}
