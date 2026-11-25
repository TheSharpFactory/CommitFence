using CF.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Settings;

/// <summary>
/// One segment of the strictness choice: a preset, or "Custom" when the file holds a mapping of its own.
/// </summary>
internal sealed partial class StrictnessChoiceViewModel : ObservableObject
{
    public StrictnessChoiceViewModel(StrictnessPreset preset, bool isEnabled = true)
    {
        Preset = preset;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Whether the segment can be chosen: the presets are off while the file holds a mapping of its own.
    /// </summary>
    public bool IsEnabled { get; }

    public StrictnessPreset Preset { get; }

    /// <summary>
    /// The segment of the strictness in force, which carries the tick.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    public string Label
    {
        get { return Preset.ToString(); }
    }

    public bool IsCustom
    {
        get { return Preset == StrictnessPreset.Custom; }
    }

    /// <summary>
    /// What the tooltip of "Custom" says: it is not edited here.
    /// </summary>
    public string? Tip
    {
        get { return IsCustom ? "Set in commitfence.json" : null; }
    }
}
