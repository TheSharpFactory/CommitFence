using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// One value of a value list: a field of its own and the button that removes it.
/// </summary>
internal sealed partial class ValueRowViewModel : ObservableObject
{
    private readonly ComparisonPickerViewModel _picker;

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial bool IsRefused { get; set; }

    public ValueRowViewModel(ComparisonPickerViewModel picker, string text)
    {
        _picker = picker;
        Text = text;
    }

    public string SpokenName
    {
        get
        {
            var rows = _picker.Rows;
            return $"Value {rows.IndexOf(this) + 1} of {rows.Count}: {Text}";
        }
    }

    public string RemoveSpokenName
    {
        get { return $"Remove {Text}"; }
    }

    public void RefreshNames()
    {
        OnPropertyChanged(nameof(SpokenName));
        OnPropertyChanged(nameof(RemoveSpokenName));
    }

    partial void OnTextChanged(string value)
    {
        IsRefused = false;
        _picker.RowTyped(this);
        RefreshNames();
    }

    [RelayCommand]
    private void Remove()
    {
        _picker.RemoveRow(this);
    }
}
