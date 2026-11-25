using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Settings;

/// <summary>
/// A row of the app's facts: its name, its value (a path in the code type, or the one switch that is kept on this machine), and a
/// second line.
/// </summary>
internal sealed partial class FactRowViewModel : ObservableObject
{
    private readonly Action<bool>? _onSwitched;

    public FactRowViewModel(string name, string value, bool isCode, string? second = null)
    {
        Name = name;
        Value = value;
        IsCode = isCode;
        Second = second;
    }

    /// <summary>
    /// A row whose value is a switch with this word; it is off or on as given, and every change is told to <paramref name="onSwitched"/>.
    /// </summary>
    public FactRowViewModel(string name, string switchLabel, bool isOn, Action<bool> onSwitched)
    {
        Name = name;
        Value = switchLabel;
        HasSwitch = true;
        _onSwitched = onSwitched;
        IsOn = isOn;
    }

    public string Name { get; }

    public string Value { get; }

    /// <summary>
    /// The value is a path or a name: it is set in the code type and breaks after a separator.
    /// </summary>
    public bool IsCode { get; }

    public string? Second { get; }

    public bool HasSwitch { get; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    public bool IsText
    {
        get { return !IsCode && !HasSwitch; }
    }

    public bool HasSecond
    {
        get { return !string.IsNullOrEmpty(Second); }
    }

    public string SpokenName
    {
        get
        {
            if(HasSwitch)
                return $"{Name}: {Value}, {(IsOn ? "on" : "off")}";

            return HasSecond ? $"{Name}: {Value} {Second}" : $"{Name}: {Value}";
        }
    }

    partial void OnIsOnChanged(bool value)
    {
        OnPropertyChanged(nameof(SpokenName));
        _onSwitched?.Invoke(value);
    }
}
