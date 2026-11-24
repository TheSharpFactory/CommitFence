using CF.App.Parts;
using CF.Core.Findings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Rules;

/// <summary>
/// One entry of the menu of an effect: block the push, or only warn.
/// </summary>
internal sealed partial class EffectMenuEntry : ObservableObject
{
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    public EffectMenuEntry(Severity severity)
    {
        Severity = severity;
    }

    public Severity Severity { get; }

    public EffectKind Kind
    {
        get
        {
            return Severity == Severity.Block ? EffectKind.Block : EffectKind.Warn;
        }
    }

    public bool IsBlock
    {
        get
        {
            return Severity == Severity.Block;
        }
    }

    public string Label
    {
        get
        {
            return Severity == Severity.Block ? "Block the push" : "Only warn";
        }
    }
}
