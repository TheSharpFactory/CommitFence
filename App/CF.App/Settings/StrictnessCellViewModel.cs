using CF.App.Parts;
using CF.Core.Findings;

namespace CF.App.Settings;

/// <summary>
/// A cell of the strictness table: what a push does for a word of the file under one column.
/// </summary>
internal sealed class StrictnessCellViewModel
{
    public StrictnessCellViewModel(Severity? effect, bool isInForce, bool isPreview = false, bool isAlwaysOff = false)
    {
        Kind = effect switch
        {
            Severity.Block => EffectKind.Block,
            Severity.Warn => EffectKind.Warn,
            _ => EffectKind.Off
        };
        IsInForce = isInForce;
        IsPreview = isPreview;
        IsAlwaysOff = isAlwaysOff;
    }

    public EffectKind Kind { get; }

    public bool IsInForce { get; }
    public bool IsPreview { get; }
    public bool IsAlwaysOff { get; }

    public string Word
    {
        get
        {
            return Kind switch
            {
                EffectKind.Block => "Blocks",
                EffectKind.Warn => "Warns",
                EffectKind.Off or EffectKind.Unset => IsAlwaysOff ? "Off, always" : "Off",
                _ => throw new ArgumentOutOfRangeException(nameof(Kind))
            };
        }
    }

    public bool IsBlock
    {
        get { return Kind == EffectKind.Block; }
    }

    public bool IsWarn
    {
        get { return Kind == EffectKind.Warn; }
    }

    public bool IsOff
    {
        get { return Kind == EffectKind.Off; }
    }
}
