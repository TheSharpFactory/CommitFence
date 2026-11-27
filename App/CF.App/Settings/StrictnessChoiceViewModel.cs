using CF.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Settings;

/// <summary>One choice of review policy and its saved selection mark.</summary>
internal sealed partial class StrictnessChoiceViewModel(StrictnessPreset preset) : ObservableObject
{
    public StrictnessPreset Preset { get; } = preset;

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    public string Label { get { return SettingsViewModel.ChoiceName(Preset); } }
}
